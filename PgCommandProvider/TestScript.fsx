#I @".\bin\Debug"

#r "FSharp.Data.PgCommandProvider.dll"

open FSharp.Data.Sql

[<Literal>]
let connectionString = "Server=127.0.0.1; Port=5432; Database=Jnx; User Id=Jnx; Password=Jnx;"

type QueryAllCountries = PgCommand<"SELECT c1.*, (select count(*) from coins_coin) as x FROM coins_country c1", connectionString>

let qry = QueryAllCountries()

printfn "Country: {name:%s;code:%s;genitive:%s;id:%d;x:%d" qry.name qry.code qry.genitive qry.id qry.x
