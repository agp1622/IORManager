using IORManager.Models;

namespace IORManager.Repositories;

public interface IFinancialDocumentRepository<TDocument>
    where TDocument : FinancialDocument
{
    IReadOnlyCollection<TDocument> GetAll();

    TDocument? GetById(Guid id);

    TDocument Add(TDocument document);
}
