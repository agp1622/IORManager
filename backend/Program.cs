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
                policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
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

        builder.Services.AddDbContext<IORManagerContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        RegisterRepositories(builder.Services);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

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
                query => query.Include(purchaseOrder => purchaseOrder.Lines));
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
