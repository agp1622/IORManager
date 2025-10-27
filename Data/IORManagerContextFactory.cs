using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IORManager.Data;

public class IORManagerContextFactory : IDesignTimeDbContextFactory<IORManagerContext>
{
    public IORManagerContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IORManagerContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=IORManager;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        return new IORManagerContext(optionsBuilder.Options);
    }
}
