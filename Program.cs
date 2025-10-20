using IORManager.Models;
using IORManager.Repositories;

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
        services.AddSingleton<IFinancialDocumentRepository<Invoice>>(_ =>
            new InMemoryFinancialDocumentRepository<Invoice>(
                new[]
                {
                    new Invoice(
                        Guid.NewGuid(),
                        "INV-001",
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                        "Acme Corp",
                        1500m,
                        new List<DocumentLine>
                        {
                            new("Consulting Services", 10, 150m)
                        })
                }));

        services.AddSingleton<IFinancialDocumentRepository<Receipt>>(_ =>
            new InMemoryFinancialDocumentRepository<Receipt>(
                new[]
                {
                    new Receipt(
                        Guid.NewGuid(),
                        "RCPT-001",
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                        "Acme Corp",
                        1500m,
                        "INV-001",
                        new List<ReceiptPayment>
                        {
                            new("Bank Transfer", 1500m)
                        })
                }));

        services.AddSingleton<IFinancialDocumentRepository<PurchaseOrder>>(_ =>
            new InMemoryFinancialDocumentRepository<PurchaseOrder>(
                new[]
                {
                    new PurchaseOrder(
                        Guid.NewGuid(),
                        "PO-001",
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
                        "Supply Co",
                        3200m,
                        new List<DocumentLine>
                        {
                            new("Laptops", 4, 800m)
                        })
                }));
    }
}
