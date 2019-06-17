open System.Diagnostics
open System.Threading
open Microsoft.ServiceFabric.Services.Runtime;
open Microsoft.Notes

[<EntryPoint>]
let main argv =
    try
      ServiceRuntime.RegisterServiceAsync("ApiType", (fun context -> new Api.Service(context) :> StatelessService)).GetAwaiter().GetResult()
      ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof<Api.Service>.Name)
      Thread.Sleep(Timeout.Infinite);
      0
    with
    | e ->
      ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString())
      reraise()
