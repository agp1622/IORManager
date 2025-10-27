using IORManager.Data;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.EntityFrameworkCore;

namespace IORManager;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
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
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<IFinancialDocumentPdfService, QuestPdfFinancialDocumentPdfService>();

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
    }
}
