using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace sqlSense.Services
{
    public interface ISimulationService
    {
        SimulationResult? ProcessSimulation(Graph graph);
    }

    public class RustSimulationService : ISimulationService
    {
        [DllImport("rust_engine.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "process_simulation")]
        private static extern IntPtr ProcessSimulationNative(string inputJson);

        [DllImport("rust_engine.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "free_string")]
        private static extern void FreeString(IntPtr ptr);

        public SimulationResult? ProcessSimulation(Graph graph)
        {
            string json = JsonConvert.SerializeObject(graph);
            IntPtr resultPtr = ProcessSimulationNative(json);
            
            try
            {
                string? resultJson = Marshal.PtrToStringAnsi(resultPtr);
                if (resultJson == null) return null;
                return JsonConvert.DeserializeObject<SimulationResult>(resultJson);
            }
            finally
            {
                FreeString(resultPtr);
            }
        }
    }

    public class Graph
    {
        [JsonProperty("nodes")]
        public System.Collections.Generic.List<Node> Nodes { get; set; } = new();
        
        [JsonProperty("edges")]
        public System.Collections.Generic.List<Edge> Edges { get; set; } = new();
    }

    public class Node
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("node_type")]
        public string NodeType { get; set; } = string.Empty;
        
        [JsonProperty("metadata")]
        public string Metadata { get; set; } = string.Empty;
    }

    public class Edge
    {
        [JsonProperty("from")]
        public string From { get; set; } = string.Empty;
        
        [JsonProperty("to")]
        public string To { get; set; } = string.Empty;
    }

    public class SimulationResult
    {
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonProperty("processed_rows")]
        public ulong ProcessedRows { get; set; }
        
        [JsonProperty("flow_data")]
        public System.Collections.Generic.List<string> FlowData { get; set; } = new();
    }
}
