using System.Collections.Concurrent;
using IORManager.Models;

namespace IORManager.Repositories;

public class InMemoryFinancialDocumentRepository<TDocument> : IFinancialDocumentRepository<TDocument>
    where TDocument : FinancialDocument
{
    private readonly ConcurrentDictionary<Guid, TDocument> _documents;

    public InMemoryFinancialDocumentRepository(IEnumerable<TDocument>? seed = null)
    {
        _documents = new ConcurrentDictionary<Guid, TDocument>(
            seed?.Select(document => new KeyValuePair<Guid, TDocument>(document.Id, document))
            ?? Array.Empty<KeyValuePair<Guid, TDocument>>());
    }

    public IReadOnlyCollection<TDocument> GetAll() => _documents.Values.ToList();

    public TDocument? GetById(Guid id)
        => _documents.TryGetValue(id, out var document) ? document : null;

    public TDocument Add(TDocument document)
    {
        if (!_documents.TryAdd(document.Id, document))
        {
            throw new InvalidOperationException($"A document with id {document.Id} already exists.");
        }

        return document;
    }
}
