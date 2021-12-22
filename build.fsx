#r "nuget: System.Reactive"
#r "nuget: Fake.Core.Target, [5.21.0-alpha003]"
#r "nuget: Fake.DotNet.Cli, [5.21.0-alpha003]"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet


do  // init fake
    let args = System.Environment.GetCommandLineArgs() |> Array.toList |> List.skip 2
    let ctx = Context.FakeExecutionContext.Create false "build.fsx" args
    Context.setExecutionContext (Context.RuntimeContext.Fake ctx)
    Target.initEnvironment()

Target.create "Build" (fun _ ->
    let options (o : DotNet.BuildOptions) =
        { o with 
            Configuration = DotNet.BuildConfiguration.Release 
        }

    "ModelLoader.fsproj" |> DotNet.build options
)

Target.create "Default" ignore


"Build" ==> "Default"

Target.runOrDefault "Default"
