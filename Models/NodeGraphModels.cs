using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace sqlSense.Models
{
    public class NodeCard
    {
        public string Id { get; set; } = "";
        public Border CardElement { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Ellipse? OutputPort { get; set; }
        public Ellipse? InputPort { get; set; }
        public List<NodeConnection> OutputConnections { get; set; } = new();
        public List<NodeConnection> InputConnections { get; set; } = new();
        public int LayoutLevel { get; set; } = 0;
        
        // Metadata for editing
        public ReferencedTable? SourceTable { get; set; }
        public JoinRelationship? JoinData { get; set; }
        public bool IsViewNode { get; set; }
        public HashSet<string> ParticipatingTables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Color { get; set; } = "#4FC3F7";
    }

    public class NodeConnection
    {
        public NodeCard Source { get; set; } = null!;
        public NodeCard Target { get; set; } = null!;
        public Path PathElement { get; set; } = null!;
        public Ellipse? StartPort { get; set; }
        public Ellipse? EndPort { get; set; }
        public Path FlowPathElement { get; set; } = null!;
        public bool IsAnimating { get; set; }
        public Border? LabelBadge { get; set; }
        public JoinRelationship? JoinData { get; set; }
        public Color Color { get; set; }
    }
}
