using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Planner_app
{
    public class JsonAppStateStorage : IAppStateStorage
    {
        private readonly string _filePath;

        public JsonAppStateStorage(string filePath)
        {
            _filePath = filePath;
        }

        public void SaveAppState(AppState state)
        {
            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public AppState LoadAppState()
        {
            if (!File.Exists(_filePath)) return new AppState();

            string json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<AppState>(json) ?? new AppState();
        }
    }

}
