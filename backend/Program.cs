using IORManager.Data;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

namespace IORManager;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers()
            // Avoid JSON serializer errors when EF navigation properties create object reference cycles
            // (e.g., Invoice -> Lines -> Invoice). Ignore cycles so repeated references are omitted.
            .AddJsonOptions(opts => opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

        const string corsPolicyName = "AllowFrontend";
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(corsPolicyName, policy =>
            {
                // Allow both dev (Vite on 5173) and IIS-deployed frontend
              var origins = new[]
                    {
                        "http://localhost:5173",
                        "http://127.0.0.1:5173",
                        "http://localhost",
                        "http://127.0.0.1",
                        "https://localhost",
                        "https://127.0.0.1",
                        "http://localhost:3001",
                        "https://localhost:3001",
                        "https://papavelagtechnology.com",
                        "https://www.papavelagtechnology.com"
                    };
                
                // Add machine hostname if available
                try
                {
                    var hostname = System.Net.Dns.GetHostName();
                    origins = origins.Concat(new[] { $"http://{hostname}", $"https://{hostname}" }).ToArray();
                }
                catch { }

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        // JWT Authentication
        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured in appsettings.");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<IFinancialDocumentPdfService, QuestPdfFinancialDocumentPdfService>();
        builder.Services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        builder.Services.AddScoped<INcfNumberGenerator, NcfNumberGenerator>();
        builder.Services.AddScoped<NcfAssignmentService>();

        builder.Services.AddDbContext<IORManagerContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Permanently purges invoices/quotes once their 1-year soft-delete recovery window elapses.
        builder.Services.AddHostedService<SoftDeletePurgeService>();

        RegisterRepositories(builder.Services);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(corsPolicyName);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IFinancialDocumentRepository<Invoice>>(provider =>
        {
            var context = provider.GetRequiredService<IORManagerContext>();
            return new EfFinancialDocumentRepository<Invoice>(
                context,
                query => query.Include(invoice => invoice.Lines));
        });

        services.AddScoped<IFinancialDocumentRepository<Quote>>(provider =>
        {
            var context = provider.GetRequiredService<IORManagerContext>();
            return new EfFinancialDocumentRepository<Quote>(
                context,
                query => query.Include(quote => quote.Lines));
        });

        services.AddScoped<IFinancialDocumentRepository<Receipt>>(provider =>
        {
            var context = provider.GetRequiredService<IORManagerContext>();
            return new EfFinancialDocumentRepository<Receipt>(
                context,
                query => query.Include(receipt => receipt.Payments));
        });

        services.AddScoped<IFinancialDocumentRepository<PurchaseOrder>>(provider =>
        {
            var context = provider.GetRequiredService<IORManagerContext>();
            return new EfFinancialDocumentRepository<PurchaseOrder>(
                context,
                query => query
                    .Include(purchaseOrder => purchaseOrder.Lines)
                    .Include(purchaseOrder => purchaseOrder.Expenses));
        });

        services.AddScoped<IFinancialDocumentRepository<AccountPayable>>(provider =>
        {
            var context = provider.GetRequiredService<IORManagerContext>();
            return new EfFinancialDocumentRepository<AccountPayable>(
                context,
                query => query.Include(ap => ap.Invoice));
        });
    }
}
