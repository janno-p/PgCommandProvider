namespace FSharp.Data.Sql

open Npgsql
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes

[<TypeProvider>]
type public PgCommandProvider() as this =
    inherit TypeProviderForNamespaces()

    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "FSharp.Data.Sql"
    let baseTy = typeof<obj>
    let staticParameters = [ProvidedStaticParameter("CommandText", typeof<string>)
                            ProvidedStaticParameter("ConnectionString", typeof<string>)]

    let pgCommandTy = ProvidedTypeDefinition(thisAssembly, rootNamespace, "PgCommand", Some baseTy)

    do this.RegisterProbingFolder @"E:\Work\PgCommandProvider\packages\Npgsql.2.2.1\lib\net45"

    do pgCommandTy.DefineStaticParameters(
        staticParameters,
        fun typeName args ->
            match args with
            | [| :? string as commandText; :? string as connectionString |] ->
                use connection = new NpgsqlConnection(connectionString)
                use command = new NpgsqlCommand(commandText, connection)
                connection.Open()
                use reader = command.ExecuteReader()

                let properties = [ for i in 1..reader.FieldCount ->
                                    ProvidedProperty(
                                        reader.GetName(i - 1),
                                        reader.GetProviderSpecificFieldType(i - 1),
                                        GetterCode = fun args -> <@@ () @@>) ]

                (*
                let prop = ProvidedProperty(
                            sprintf "WillGive%d" value,
                            typeof<int>,
                            GetterCode = fun args -> <@@ value @@>)
                *)

                let ty = ProvidedTypeDefinition(thisAssembly, rootNamespace, typeName, Some baseTy)
                ty.AddMember(ProvidedConstructor([], InvokeCode = fun args -> <@@ () @@>))
                ty.AddMembers(properties)
                ty
            | _ -> failwith "Unexpected parameter values"
        )

    do this.AddNamespace(rootNamespace, [pgCommandTy])

[<TypeProviderAssembly>]
do ()
