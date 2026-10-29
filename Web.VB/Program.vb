
Imports System.Text
Imports Ardalis.ListStartupServices
Imports MediatR
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Diagnostics.HealthChecks
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.AspNetCore.Mvc.ApplicationModels
Imports Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.eShopWeb.ApplicationCore
Imports Microsoft.eShopWeb.ApplicationCore.Interfaces
Imports Microsoft.eShopWeb.ApplicationCore.Services
Imports Microsoft.eShopWeb.Infrastructure.Data
Imports Microsoft.eShopWeb.Infrastructure.Identity
Imports Microsoft.eShopWeb.Infrastructure.Logging
Imports Microsoft.eShopWeb.Infrastructure.Services
Imports Microsoft.eShopWeb.Web.Interfaces
Imports Microsoft.eShopWeb.Web.Services
Imports Microsoft.eShopWeb.Web.ViewModels
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Diagnostics.HealthChecks
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Newtonsoft.Json
Imports System.Net.Mime

Public Class Program
    Public Shared Sub Main(ByVal args As String())
        Dim builder = WebApplication.CreateBuilder(args)

        Dim configuration As IConfiguration = builder.Configuration
        Dim services = builder.Services

        ' Database providers: in-memory for Development/Testing, SQL Server otherwise.
        ' Deliberate improvement over the baseline Startup convention: baseline relied on
        ' Configure{Environment}Services, so an environment such as "Staging" matched no
        ' method, registered NO DbContext and failed at first resolve. Every non
        ' Development/Testing environment now gets the SQL Server registration.
        If builder.Environment.IsDevelopment() OrElse builder.Environment.IsEnvironment("Testing") Then
            ConfigureInMemoryDatabases(services)
        Else
            ConfigureProductionDatabases(services, configuration)
        End If

        ConfigureServices(services, configuration)

        Dim app = builder.Build()

        Configure(app, app.Environment)

        SeedDatabases(app)

        app.Run()
    End Sub

    Private Shared Sub ConfigureInMemoryDatabases(ByVal services As IServiceCollection)
        services.AddDbContext(Of CatalogContext)(Function(c) c.UseInMemoryDatabase("Catalog"))
        services.AddDbContext(Of AppIdentityDbContext)(Function(options) options.UseInMemoryDatabase("Identity"))
    End Sub

    Private Shared Sub ConfigureProductionDatabases(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        services.AddDbContext(Of CatalogContext)(Function(c) c.UseSqlServer(configuration.GetConnectionString("CatalogConnection")))
        services.AddDbContext(Of AppIdentityDbContext)(Function(options) options.UseSqlServer(configuration.GetConnectionString("IdentityConnection")))
    End Sub

    Public Shared Sub ConfigureServices(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        ConfigureCookieSettings(services)
        CreateIdentityIfNotCreated(services)
        services.AddMediatR(Sub(cfg) cfg.RegisterServicesFromAssembly(GetType(BasketViewModelService).Assembly))
        services.AddScoped(GetType(IAsyncRepository(Of)), GetType(EfRepository(Of)))
        services.AddScoped(Of ICatalogViewModelService, CachedCatalogViewModelService)()
        services.AddScoped(Of IBasketService, BasketService)()
        services.AddScoped(Of IBasketViewModelService, BasketViewModelService)()
        services.AddScoped(Of IOrderService, OrderService)()
        services.AddScoped(Of IOrderRepository, OrderRepository)()
        services.AddScoped(Of CatalogViewModelService)()
        services.AddScoped(Of ICatalogItemViewModelService, CatalogItemViewModelService)()
        services.Configure(Of CatalogSettings)(configuration)
        services.AddSingleton(Of IUriComposer)(New UriComposer(configuration.[Get](Of CatalogSettings)()))
        services.AddScoped(GetType(IAppLogger(Of)), GetType(LoggerAdapter(Of)))
        services.AddTransient(Of IEmailSender, EmailSender)()
        services.AddMemoryCache()
        services.AddRouting(
                 Sub(options) options.ConstraintMap("slugify") = GetType(SlugifyParameterTransformer)
            )
        services.AddMvc(
                Sub(options) options.Conventions.Add(New RouteTokenTransformerConvention(New SlugifyParameterTransformer()))
          )
        services.AddRazorPages(
               Sub(options) options.Conventions.AuthorizePage("/Basket/Checkout")
            )
        services.AddControllersWithViews(). ' Enable Vazor
        AddRazorRuntimeCompilation(
             Sub(options) options.FileProviders.Add(New Vazor.VazorViewProvider())
        )
        services.AddHttpContextAccessor()
        services.AddSwaggerGen(Sub(c) c.SwaggerDoc("v1", New Microsoft.OpenApi.OpenApiInfo With {
        .Title = "My API",
        .Version = "v1"
    }))
        services.AddHealthChecks()
        services.Configure(Of ServiceConfig)(Sub(config)
                                                 config.Services = New List(Of ServiceDescriptor)(services)
                                                 config.Path = "/allservices"
                                             End Sub)
    End Sub

    Private Shared Sub CreateIdentityIfNotCreated(ByVal services As IServiceCollection)
        Dim sp = services.BuildServiceProvider()

        Using scope = sp.CreateScope()
            Dim existingUserManager = scope.ServiceProvider.GetService(Of UserManager(Of ApplicationUser))()

            If existingUserManager Is Nothing Then
                services.AddIdentity(Of ApplicationUser, IdentityRole)().AddDefaultUI().AddEntityFrameworkStores(Of AppIdentityDbContext)().AddDefaultTokenProviders()
            End If
        End Using
    End Sub

    Private Shared Sub ConfigureCookieSettings(ByVal services As IServiceCollection)
        services.Configure(Of CookiePolicyOptions)(Sub(options)
                                                       options.CheckConsentNeeded = Function(context) True
                                                       options.MinimumSameSitePolicy = SameSiteMode.None
                                                   End Sub)
        services.ConfigureApplicationCookie(Sub(options)
                                                options.Cookie.HttpOnly = True
                                                options.ExpireTimeSpan = TimeSpan.FromHours(1)
                                                options.LoginPath = "/Identity/Account/Login"
                                                options.LogoutPath = "/Identity/Account/Logout"
                                                options.Cookie = New CookieBuilder With {
                                                    .IsEssential = True
                                                }
                                            End Sub)
    End Sub

    Public Shared Sub Configure(ByVal app As WebApplication, ByVal env As IWebHostEnvironment)

        ZML.ZmlPages.Compile() ' Erase this statement when you fisnish converting all .zml files to vazor files
        Vazor.VazorSharedView.CreateAll()

        app.UseHealthChecks("/health", New HealthCheckOptions With {
            .ResponseWriter = Async Function(context, report)
                                  Dim result = JsonConvert.SerializeObject(New With {
                                               Key .status = report.Status.ToString(),
                                               Key .errors = report.Entries.[Select](Function(e) New With {
                                               Key .key = e.Key,
                                               Key .value = [Enum].GetName(GetType(HealthStatus), e.Value.Status)
                                          })
                                      })
                                  context.Response.ContentType = MediaTypeNames.Application.Json
                                  Await context.Response.WriteAsync(result)
                              End Function
        })

        If env.IsDevelopment() Then
            app.UseDeveloperExceptionPage()
            app.UseShowAllServicesMiddleware()
            app.UseMigrationsEndPoint()
        Else
            app.UseExceptionHandler("/Error")
            app.UseHsts()
        End If

        app.UseStaticFiles()
        app.UseRouting()
        app.UseHttpsRedirection()
        app.UseCookiePolicy()
        app.UseAuthentication()
        app.UseAuthorization()
        app.UseSwagger()
        app.UseSwaggerUI(Sub(c)
                             c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1")
                         End Sub)

        app.MapControllerRoute("default", "{controller:slugify=Home}/{action:slugify=Index}/{id?}")
        app.MapRazorPages()
        app.MapHealthChecks("home_page_health_check")
        app.MapHealthChecks("api_health_check")
    End Sub

    Private Shared Sub SeedDatabases(ByVal app As WebApplication)
        ' Startup-only: VB has no async entry point, so the async seeding path is
        ' awaited here via GetAwaiter().GetResult(). This replaces the previous
        ' Task.Run(Sub() ...SeedAsync(...)).Wait() which never awaited the returned
        ' Task (fire-and-forget: the app could start before seeding completed).
        SeedDatabasesAsync(app).GetAwaiter().GetResult()
    End Sub

    Private Shared Async Function SeedDatabasesAsync(ByVal app As WebApplication) As Task
        Using scope = app.Services.CreateScope()
            Dim Services = scope.ServiceProvider
            Dim LoggerFactory = Services.GetRequiredService(Of ILoggerFactory)()
            Try
                Dim CatalogContext = Services.GetRequiredService(Of CatalogContext)()
                Await CatalogContextSeed.SeedAsync(CatalogContext, LoggerFactory)

                Dim UserManager = Services.GetRequiredService(Of UserManager(Of ApplicationUser))()
                Dim RoleManager = Services.GetRequiredService(Of RoleManager(Of IdentityRole))()
                Await AppIdentityDbContextSeed.SeedAsync(UserManager, RoleManager)
            Catch ex As Exception
                Dim Logger = LoggerFactory.CreateLogger(Of Program)()
                Logger.LogError(ex, "An error occurred seeding the DB.")
            End Try
        End Using
    End Function
End Class
