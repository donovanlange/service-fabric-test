namespace Microsoft.Notes

open System
open System.Fabric
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.ServiceFabric.Services.Communication.AspNetCore
open Microsoft.ServiceFabric.Services.Communication.Runtime
open Microsoft.ServiceFabric.Services.Runtime
open Giraffe

module Api =
  let webApp =
      choose [
          GET >=>
              choose [
                  route "/" >=> text "Hello world!"
              ]
          setStatusCode 404 >=> text "Not Found" ]

  let configureApp (app : IApplicationBuilder) =
      let errorHandler (ex : Exception) (logger : ILogger) =
          logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
          clearResponse >=> setStatusCode 500 >=> text ex.Message

      let configureCors (builder : CorsPolicyBuilder) =
          builder.WithOrigins("http://localhost:8080")
                 .AllowAnyMethod()
                 .AllowAnyHeader()
                 |> ignore

      let env = app.ApplicationServices.GetService<IHostingEnvironment>()
      (match env.IsDevelopment() with
      | true  -> app.UseDeveloperExceptionPage()
      | false -> app.UseGiraffeErrorHandler errorHandler)
          .UseCors(configureCors)
          .UseGiraffe(webApp)

  let configureServices (serviceContext: StatelessServiceContext) (services : IServiceCollection) =
      services.AddSingleton serviceContext |> ignore
      services.AddCors()                   |> ignore
      services.AddGiraffe()                |> ignore

  let configureLogging (builder : ILoggingBuilder) =
      builder.AddFilter(fun l -> l.Equals LogLevel.Error)
             .AddConsole()
             .AddDebug() |> ignore


  type Service(context: StatelessServiceContext) =
    inherit StatelessService(context)

    override __.CreateServiceInstanceListeners() = Array.toSeq [|
      new ServiceInstanceListener (fun serviceContext ->
        new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (fun url listener ->
            ServiceEventSource.Current.ServiceMessage(serviceContext, "Starting Kestrel on {url}")
            WebHostBuilder()
              .UseKestrel()
              .Configure(Action<IApplicationBuilder> configureApp)
              .ConfigureServices(configureServices serviceContext)
              .ConfigureLogging(configureLogging)
              .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
              .UseUrls(url)
              .Build())) :> ICommunicationListener)
      |]