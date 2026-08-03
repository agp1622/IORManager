using IORManager.Models;
using IORManager.Services;
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
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();
    public DbSet<ReceiptPayment> ReceiptPayments => Set<ReceiptPayment>();
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();
    public DbSet<NcfSequence> NcfSequences => Set<NcfSequence>();
    public DbSet<FiscalRegime> FiscalRegimes => Set<FiscalRegime>();
    public DbSet<PurchaseOrderAttachment> PurchaseOrderAttachments => Set<PurchaseOrderAttachment>();
    public DbSet<QuoteAttachment> QuoteAttachments => Set<QuoteAttachment>();
    public DbSet<AccountPayable> AccountsPayable => Set<AccountPayable>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EcfRange> EcfRanges => Set<EcfRange>();
    public DbSet<EcfSubmission> EcfSubmissions => Set<EcfSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureFinancialDocuments(modelBuilder);
        ConfigureCustomers(modelBuilder);
        ConfigureDocumentLines(modelBuilder);
        ConfigureQuotes(modelBuilder);
        ConfigureInvoices(modelBuilder);
        ConfigureReceipts(modelBuilder);
        ConfigureInvoiceNumberSequence(modelBuilder);
        ConfigureNcfSequence(modelBuilder);
        ConfigureFiscalRegime(modelBuilder);
        ConfigurePurchaseOrderAttachment(modelBuilder);
        ConfigureQuoteAttachment(modelBuilder);
        ConfigurePurchaseOrderQuoteLink(modelBuilder);
        ConfigureAccountPayable(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureEcfRange(modelBuilder);
        ConfigureEcfSubmission(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Email).IsUnique();
        });
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
                .HasValue<Quote>("Quote")
                .HasValue<Invoice>("Invoice")
                .HasValue<PurchaseOrder>("PurchaseOrder")
                .HasValue<Receipt>("Receipt")
                .HasValue<AccountPayable>("AccountPayable");

            builder.HasIndex(document => document.Number)
                .IsUnique();

            builder.Property(document => document.Date)
                .HasConversion(converter)
                .Metadata.SetValueComparer(comparer);

            // Soft-deleted documents (DeletedAt set) are hidden from all normal queries.
            // Use IgnoreQueryFilters() to reach trashed/recoverable records (e.g. trash listings,
            // restore actions, and the background purge job).
            builder.HasQueryFilter(document => document.DeletedAt == null);
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

            builder.HasOne(line => line.Quote)
                .WithMany(quote => quote.Lines)
                .HasForeignKey(line => line.QuoteId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(line => line.PurchaseOrder)
                .WithMany(purchaseOrder => purchaseOrder.Lines)
                .HasForeignKey(line => line.PurchaseOrderId)
                // Prevent multiple cascade paths to FinancialDocument table on SQL Server.
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureCustomers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(builder =>
        {
            builder.HasIndex(customer => customer.Name)
                .IsUnique();
        });
    }

    private static void ConfigureInvoices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(builder =>
        {
            builder.HasIndex(invoice => invoice.NcfNumber)
                .IsUnique()
                .HasFilter("[NcfNumber] IS NOT NULL");

            builder.Property(invoice => invoice.NcfCategory)
                .HasMaxLength(10);

            builder.HasOne(invoice => invoice.Customer)
                .WithMany(customer => customer.Invoices)
                .HasForeignKey(invoice => invoice.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(invoice => invoice.QuoteId)
                .IsUnique()
                .HasFilter("[QuoteId] IS NOT NULL");

            var expirationConverter = new ValueConverter<DateOnly?, DateTime?>(
                dateOnly => dateOnly.HasValue ? dateOnly.Value.ToDateTime(TimeOnly.MinValue) : null,
                dateTime => dateTime.HasValue
                    ? DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc))
                    : null);

            var expirationComparer = new ValueComparer<DateOnly?>(
                (left, right) => left == right,
                dateOnly => dateOnly.HasValue ? dateOnly.Value.GetHashCode() : 0,
                dateOnly => dateOnly);

            builder.Property(invoice => invoice.ExpirationDateOverride)
                .HasConversion(expirationConverter)
                .Metadata.SetValueComparer(expirationComparer);
        });
    }

    private static void ConfigureQuotes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>(builder =>
        {
            builder.HasOne(quote => quote.Customer)
                .WithMany()
                .HasForeignKey(quote => quote.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(quote => quote.ConvertedInvoiceId)
                .IsUnique()
                .HasFilter("[ConvertedInvoiceId] IS NOT NULL");
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

    private static void ConfigureNcfSequence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NcfSequence>(builder =>
        {
            builder.HasKey(sequence => sequence.Id);
            builder.Property(sequence => sequence.Id).ValueGeneratedNever();
            builder.HasIndex(sequence => sequence.CategoryCode)
                .IsUnique();
            builder.Property(sequence => sequence.CategoryCode)
                .HasMaxLength(10)
                .IsRequired();
            builder.HasData(new NcfSequence
            {
                Id = 1,
                CategoryCode = NcfCategoryCatalog.DefaultCategoryCode,
                NextNumber = 1
            });
        });
    }

    private static void ConfigurePurchaseOrderAttachment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrderAttachment>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(a => a.FileData).HasColumnType("varbinary(max)").IsRequired();

            builder.HasOne(a => a.PurchaseOrder)
                .WithMany(po => po.Attachments)
                .HasForeignKey(a => a.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureQuoteAttachment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuoteAttachment>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(a => a.FileData).HasColumnType("varbinary(max)").IsRequired();

            builder.HasOne(a => a.Quote)
                .WithMany(q => q.Attachments)
                .HasForeignKey(a => a.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePurchaseOrderQuoteLink(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrder>(builder =>
        {
            builder.Property(po => po.QuoteId).IsRequired(false);

            // Prevent multiple cascade paths on FinancialDocument (self-referential FK).
            builder.HasOne(po => po.Quote)
                .WithMany()
                .HasForeignKey(po => po.QuoteId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureFiscalRegime(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FiscalRegime>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Code).HasMaxLength(10).IsRequired();
            builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
            builder.HasIndex(r => r.Code).IsUnique();

            builder.HasMany(r => r.Invoices)
                .WithOne(i => i.FiscalRegime)
                .HasForeignKey(i => i.FiscalRegimeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Seed all DR NCF fiscal regimes derived from the catalog
            var seedData = NcfCategoryCatalog.GetAll()
                .Select((definition, index) => new FiscalRegime
                {
                    Id = index + 1,
                    Code = definition.Code,
                    Name = definition.Name,
                    InvoiceCount = 0
                })
                .ToArray();

            builder.HasData(seedData);
        });
    }

    private static void ConfigureAccountPayable(ModelBuilder modelBuilder)
    {
        var dueDateConverter = new ValueConverter<DateOnly, DateTime>(
            dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            dateTime => DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)));

        var dueDateComparer = new ValueComparer<DateOnly>(
            (left, right) => left == right,
            dateOnly => dateOnly.GetHashCode(),
            dateOnly => dateOnly);

        modelBuilder.Entity<AccountPayable>(builder =>
        {
            builder.Property(ap => ap.DueDate)
                .HasConversion(dueDateConverter)
                .Metadata.SetValueComparer(dueDateComparer);

            builder.Property(ap => ap.Status)
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(ap => ap.CustomerPO)
                .HasMaxLength(100);

            builder.Property(ap => ap.Notes)
                .HasMaxLength(1000);

            builder.Property(ap => ap.InvoiceId)
                .IsRequired(false);

            // Prevent multiple cascade paths through FinancialDocument TPH table.
            builder.HasOne(ap => ap.Invoice)
                .WithMany()
                .HasForeignKey(ap => ap.InvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(ap => ap.InvoiceId)
                .IsUnique()
                .HasFilter("[InvoiceId] IS NOT NULL");
        });
    }

    private static void ConfigureEcfRange(ModelBuilder modelBuilder)
    {
        var dateConverter = new ValueConverter<DateOnly?, DateTime?>(
            dateOnly => dateOnly.HasValue ? dateOnly.Value.ToDateTime(TimeOnly.MinValue) : null,
            dateTime => dateTime.HasValue
                ? DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc))
                : null);

        var dateComparer = new ValueComparer<DateOnly?>(
            (left, right) => left == right,
            dateOnly => dateOnly.HasValue ? dateOnly.Value.GetHashCode() : 0,
            dateOnly => dateOnly);

        modelBuilder.Entity<EcfRange>(builder =>
        {
            builder.HasIndex(range => range.DocumentTypeCode).IsUnique();

            builder.Property(range => range.AuthorizedAt)
                .HasConversion(dateConverter)
                .Metadata.SetValueComparer(dateComparer);

            builder.Property(range => range.ExpiresAt)
                .HasConversion(dateConverter)
                .Metadata.SetValueComparer(dateComparer);
        });
    }

    private static void ConfigureEcfSubmission(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EcfSubmission>(builder =>
        {
            builder.HasIndex(submission => submission.InvoiceId).IsUnique();
            builder.HasIndex(submission => submission.ENcf).IsUnique();

            builder.HasOne(submission => submission.Invoice)
                .WithMany()
                .HasForeignKey(submission => submission.InvoiceId)
                // Prevent multiple cascade paths through the FinancialDocument TPH table.
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
