using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planner_app
{
    public interface IAppStateStorage
    {
        void SaveAppState(AppState state);
        AppState LoadAppState();
    }
}
