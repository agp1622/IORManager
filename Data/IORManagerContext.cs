using IORManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IORManager.Data;

public class IORManagerContext : DbContext
{
    public IORManagerContext(DbContextOptions<IORManagerContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();
    public DbSet<ReceiptPayment> ReceiptPayments => Set<ReceiptPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureFinancialDocuments(modelBuilder);
        ConfigureDocumentLines(modelBuilder);
        ConfigureReceipts(modelBuilder);
    }

    private static void ConfigureFinancialDocuments(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<DateOnly, DateTime>(
            dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            dateTime => DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)));

        var comparer = new ValueComparer<DateOnly>(
            (left, right) => left == right,
            dateOnly => dateOnly.GetHashCode(),
            dateOnly => dateOnly);

        modelBuilder.Entity<FinancialDocument>(builder =>
        {
            builder.HasDiscriminator<string>("DocumentType")
                .HasValue<Invoice>("Invoice")
                .HasValue<PurchaseOrder>("PurchaseOrder")
                .HasValue<Receipt>("Receipt");

            builder.Property(document => document.Date)
                .HasConversion(converter)
                .Metadata.SetValueComparer(comparer);
        });
    }

    private static void ConfigureDocumentLines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentLine>(builder =>
        {
            builder.HasOne(line => line.Invoice)
                .WithMany(invoice => invoice.Lines)
                .HasForeignKey(line => line.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(line => line.PurchaseOrder)
                .WithMany(purchaseOrder => purchaseOrder.Lines)
                .HasForeignKey(line => line.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureReceipts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Receipt>(builder =>
        {
            builder.HasMany(receipt => receipt.Payments)
                .WithOne(payment => payment.Receipt)
                .HasForeignKey(payment => payment.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
