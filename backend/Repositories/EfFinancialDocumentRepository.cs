using IORManager.Data;
using IORManager.Models;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Repositories;

public class EfFinancialDocumentRepository<TDocument> : IFinancialDocumentRepository<TDocument>
    where TDocument : FinancialDocument
{
    private readonly IORManagerContext _context;
    private readonly DbSet<TDocument> _set;
    private readonly Func<IQueryable<TDocument>, IQueryable<TDocument>> _include;

    public EfFinancialDocumentRepository(
        IORManagerContext context,
        Func<IQueryable<TDocument>, IQueryable<TDocument>> include)
    {
        _context = context;
        _set = context.Set<TDocument>();
        _include = include;
    }

    public IReadOnlyCollection<TDocument> GetAll()
        => _include(_set).AsNoTracking().ToList();

    public TDocument? GetById(Guid id)
        => _include(_set.Where(document => document.Id == id))
            .AsNoTracking()
            .FirstOrDefault();

    public TDocument Add(TDocument document)
    {
        _set.Add(document);
        _context.SaveChanges();
        return document;
    }
}
