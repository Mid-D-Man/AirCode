
// Services/SvgIcon/ISvgIconService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AirCode.Services.VisualElements
{
    public interface ISvgIconService
    {
        Task<string> GetSvgContentAsync(string iconName);
        Task<IEnumerable<string>> GetAvailableIconNamesAsync();
        bool IconExists(string iconName);
    }
}
