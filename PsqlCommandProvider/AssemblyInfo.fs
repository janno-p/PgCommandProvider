module PsqlCommandProvider.AssemblyInfo

open Microsoft.FSharp.Core.CompilerServices
open System.Reflection
open System.Runtime.CompilerServices

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.0"

[<assembly: AssemblyDescription("PostgreSQL client F# providers")>]
[<assembly: AssemblyFileVersion(AssemblyVersionInformation.Version)>]
[<assembly: AssemblyProduct("PsqlCommandProvider")>]
[<assembly: AssemblyTitle("PsqlCommandProvider")>]
[<assembly: AssemblyVersion(AssemblyVersionInformation.Version)>]
[<assembly: TypeProviderAssembly()>]
()

// The assembly version has the format {Major}.{Minor}.{Build}.{Revision}
//[<assembly: AssemblyDelaySign(false)>]
//[<assembly: AssemblyKeyFile("")>]
