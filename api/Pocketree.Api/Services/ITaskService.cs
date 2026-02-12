using System.Collections.Generic;
using System.Threading.Tasks;
using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using Task = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Services
{
    public interface ITaskService
    {
        System.Threading.Tasks.Task CleanupOldTasks(User user);
        System.Threading.Tasks.Task<List<Task>> FetchNewTasks(User user);
    }
}
