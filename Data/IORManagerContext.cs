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
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureFinancialDocuments(modelBuilder);
        ConfigureDocumentLines(modelBuilder);
        ConfigureReceipts(modelBuilder);
        ConfigureInvoiceNumberSequence(modelBuilder);
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
                // Prevent multiple cascade paths to FinancialDocument table on SQL Server.
                // Use NoAction so the DB won't create ON DELETE CASCADE here. If parent deletion should remove
                // children, perform explicit deletes in code or create a separate cleanup migration/trigger.
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(line => line.PurchaseOrder)
                .WithMany(purchaseOrder => purchaseOrder.Lines)
                .HasForeignKey(line => line.PurchaseOrderId)
                // Prevent multiple cascade paths to FinancialDocument table on SQL Server.
                .OnDelete(DeleteBehavior.NoAction);
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

    private static void ConfigureInvoiceNumberSequence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceNumberSequence>(builder =>
        {
            builder.HasKey(sequence => sequence.Id);
            builder.Property(sequence => sequence.Id).ValueGeneratedNever();
            builder.HasData(new InvoiceNumberSequence { Id = 1, NextNumber = 1 });
        });
    }
}
