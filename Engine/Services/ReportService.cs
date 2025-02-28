using System.Threading.Tasks;
using Engine.Models;

namespace Engine.Services;

public interface IReportService
{
    Task<ReportModel> CreateAsync();
    Task<bool> UpdateAsync(long reportId, ReportModel newModel);
    Task<bool> DeleteAsync(long reportId);
    Task<ReportModel> GetAsync(long reportId);
}

public class ReportService: IReportService
{
    private readonly DbContext _dbContext;

    public ReportService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ReportModel> CreateAsync()
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> UpdateAsync(long reportId, ReportModel newModel)
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> DeleteAsync(long reportId)
    {
        throw new System.NotImplementedException();
    }

    public Task<ReportModel> GetAsync(long reportId)
    {
        throw new System.NotImplementedException();
    }
}