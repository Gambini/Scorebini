using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScorebiniTwitchApi.Services;
using System.Net;

namespace ScorebiniTwitchApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(
                options =>
                {
                    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
                }
            );


            builder.Services.AddOptions<TwitchOptions>()
                .Bind(builder.Configuration.GetSection(TwitchOptions.Section))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton<IValidateOptions<TwitchOptions>, TwitchOptionsValidation>();

            builder.Services.AddSingleton<TwitchAppTokenService>();
            builder.Services.AddHostedService<TokenValidationBackgroundService>();
            builder.Services.AddSingleton<TokenRefreshService>(); // need this for DI purposes
            builder.Services.AddHostedService<TokenRefreshService>( // and this for auto-start purposes
                provider => provider.GetRequiredService<TokenRefreshService>());
            builder.Services.AddHttpClient();

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(builder.Configuration.GetConnectionString("AppDb"));
            });

            var app = builder.Build();
            // nginx
            app.UseForwardedHeaders(new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else // non-dev mode
            {
                app.UseHttpsRedirection(); // enforce https
            }

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Database.EnsureCreated(); // Note: this deletes and recreates database on model changes
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}