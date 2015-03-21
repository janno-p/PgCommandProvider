namespace FSharp.Data.Psql

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Npgsql
open ProviderImplementation.ProvidedTypes
open System.Reflection
open System.Text
open System.Text.RegularExpressions

type Entity() =
    let data = System.Collections.Generic.Dictionary<string,obj>()
    member __.GetValue(key) = match data.TryGetValue key with true, value -> value | _ -> null

[<TypeProvider>]
type PsqlCommandProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let namespaceName = this.GetType().Namespace
    let runtimeAssembly = Assembly.LoadFrom(config.RuntimeAssembly)

    do
        let providerType = ProvidedTypeDefinition(runtimeAssembly, namespaceName, "PsqlCommand", Some typeof<obj>, HideObjectMethods=true)

        providerType.DefineStaticParameters(
            [ ProvidedStaticParameter("CommandText", typeof<string>)
              ProvidedStaticParameter("ConnectionString", typeof<string>) ],
            fun typeName args -> this.CreateCommandType(typeName, unbox args.[0], unbox args.[1])
        )

        this.AddNamespace(namespaceName, [providerType])

    member internal __.CreateCommandType(typeName, sql, connectionString) =
        let commandType = ProvidedTypeDefinition(runtimeAssembly, namespaceName, typeName, Some typeof<obj>, HideObjectMethods=true)

        use connection = new NpgsqlConnection(connectionString: string)

        let prepareName = "qry" + System.Guid.NewGuid().ToString().Replace("-", "").ToLower()

        let regex = Regex(@":\w+", RegexOptions.Compiled ||| RegexOptions.Multiline)

        let parameters = [ for m in regex.Matches(sql) -> m.Value ] |> Set.ofList |> List.ofSeq
        let prepareQuery =
            let sb = parameters
                     |> List.fold (fun (sb: StringBuilder, i) x -> sb.Replace(x, sprintf "$%d" i), i + 1)
                                  (StringBuilder(sql), 1)
                     |> fst
            sb.ToString()

        let commandText = sprintf "PREPARE %s AS %s" prepareName prepareQuery
        use command = new NpgsqlCommand(commandText, connection)

        connection.Open()
        command.ExecuteNonQuery() |> ignore

        let paramText = sprintf "SELECT parameter_types FROM pg_prepared_statements WHERE name = '%s'" prepareName
        use paramCommand = new NpgsqlCommand(paramText, connection)
        let parameterTypes = paramCommand.ExecuteScalar()

        let pty = match parameterTypes with
                  | :? string as str -> str.Replace("{", "").Replace("}", "").Split(',') |> Array.toList
                  | _ -> []

        let matchType = function
            | "text" -> typeof<string>
            | "integer" -> typeof<int32>
            | x -> failwithf "Unknown type %s" x

        let mp = (parameters, pty) ||> List.mapi2 (fun i p pt -> ProvidedParameter(p.Replace(":", ""), matchType pt))

        use transaction = connection.BeginTransaction()

        use action = new NpgsqlCommand(sql, connection, transaction)
        parameters |> List.iter (fun s -> action.Parameters.Add(NpgsqlParameter(s.Replace(":", ""), null)) |> ignore)

        let resultType =
            use reader = action.ExecuteReader()
            match reader.FieldCount with
            | 0 ->
                typeof<unit>
            | n ->
                let resultType = ProvidedTypeDefinition("Result", Some typeof<obj>, HideObjectMethods=true)
                for i in 0 .. n - 1 do
                    let name = reader.GetName(i)
                    let typ = reader.GetFieldType(i)
                    let prop = ProvidedProperty(name, typ)
                    prop.GetterCode <- (fun args -> Expr.Coerce(<@@ ((%%args.[0]: obj) :?> Entity).GetValue(name) @@>, typ))
                    resultType.AddMember(prop)
                commandType.AddMember(resultType)
                resultType :> System.Type

        transaction.Rollback()

        commandType.AddMember(ProvidedConstructor([], InvokeCode = (fun _ -> <@@ () @@>)))
        commandType.AddMember(ProvidedMethod("Execute", mp, resultType, InvokeCode = (fun _ -> <@@ Entity() @@>)))

        commandType
