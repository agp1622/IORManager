-- Migrate all data from IORManager_dev to IORManager_prod
-- Run this against the destination database (IORManager_prod)

USE IORManager_prod;

-- Disable foreign key constraints temporarily
ALTER TABLE DocumentLines NOCHECK CONSTRAINT ALL;
ALTER TABLE ReceiptPayments NOCHECK CONSTRAINT ALL;
ALTER TABLE FinancialDocument NOCHECK CONSTRAINT ALL;

-- Clear existing data (in reverse dependency order)
DELETE FROM ReceiptPayments;
DELETE FROM DocumentLines;
DELETE FROM FinancialDocument;
DELETE FROM Customers;
DELETE FROM InvoiceNumberSequences;
DELETE FROM NcfSequences;

-- Copy sequence data
INSERT INTO InvoiceNumberSequences (Id, NextNumber)
SELECT Id, NextNumber FROM IORManager_dev.dbo.InvoiceNumberSequences;

INSERT INTO NcfSequences (Id, CategoryCode, NextNumber)
SELECT Id, CategoryCode, NextNumber FROM IORManager_dev.dbo.NcfSequences;

-- Copy customers
INSERT INTO Customers (Id, Name, Address, Contact, CreatedAt, UpdatedAt)
SELECT Id, Name, Address, Contact, CreatedAt, UpdatedAt FROM IORManager_dev.dbo.Customers;

-- Copy financial documents (Invoices, Quotes, PurchaseOrders, Receipts)
INSERT INTO FinancialDocument
  (Id, DocumentType, Number, Date, PartyName, TotalAmount, CurrencyCode, CultureName,
   CustomerAddress, CustomerContact, CustomerId, ExpirationDateOverride, InvoiceGeneratedAt,
   ItbisRate, NcfCategory, NcfNumber, QuoteId, ConvertedAt, ConvertedInvoiceId, ReferenceNumber, Quote_CustomerAddress, Quote_CustomerContact, Quote_CustomerId, Quote_ItbisRate)
SELECT
  Id, DocumentType, Number, Date, PartyName, TotalAmount, CurrencyCode, CultureName,
  CustomerAddress, CustomerContact, CustomerId, ExpirationDateOverride, InvoiceGeneratedAt,
  ItbisRate, NcfCategory, NcfNumber, QuoteId, ConvertedAt, ConvertedInvoiceId, ReferenceNumber, Quote_CustomerAddress, Quote_CustomerContact, Quote_CustomerId, Quote_ItbisRate
FROM IORManager_dev.dbo.FinancialDocument;

-- Copy document lines
INSERT INTO DocumentLines (Id, InvoiceId, QuoteId, PurchaseOrderId, Description, Quantity, UnitPrice, UnitOfMeasure)
SELECT Id, InvoiceId, QuoteId, PurchaseOrderId, Description, Quantity, UnitPrice, UnitOfMeasure
FROM IORManager_dev.dbo.DocumentLines;

-- Copy receipt payments
INSERT INTO ReceiptPayments (Id, ReceiptId, Amount, Method)
SELECT Id, ReceiptId, Amount, Method FROM IORManager_dev.dbo.ReceiptPayments;

-- Re-enable foreign key constraints
ALTER TABLE FinancialDocument CHECK CONSTRAINT ALL;
ALTER TABLE DocumentLines CHECK CONSTRAINT ALL;
ALTER TABLE ReceiptPayments CHECK CONSTRAINT ALL;

PRINT 'Data migration completed successfully!';
