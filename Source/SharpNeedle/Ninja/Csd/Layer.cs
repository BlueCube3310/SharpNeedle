﻿namespace SharpNeedle.Ninja.Csd;
using Motions;

public class Layer : IBinarySerializable<Scene>, IList<Cast>
{
    public int Count => Children.Count;
    public bool IsReadOnly => false;

    private List<Cast> CastBuffer { get; set; } = new();
    private List<Cast> Children { get; set; } = new();
    public IReadOnlyList<Cast> Casts => CastBuffer;
    public Scene Scene { get; set; }

    public void AttachMotion(LayerMotion motion)
    {
        motion.Layer = this;
        motion.OnAttach(this);
    }

    public void Read(BinaryObjectReader reader, Scene context)
    {
        Scene = context;
        var castCount = reader.Read<int>();
        CastBuffer = new List<Cast>(castCount);
        reader.ReadOffset(() =>
        {
            for (int i = 0; i < castCount; i++)
                CastBuffer.Add(reader.ReadObjectOffset<Cast, Layer>(this));
        });
        
        var root = reader.Read<int>();
        var tree = reader.ReadArrayOffset<TreeDescriptorNode>(CastBuffer.Count);
        if (CastBuffer.Count != 0)
            ParseTree(root);

        Children = new List<Cast>(CastBuffer.Count);
        for (var i = 0; i < CastBuffer.Count; i++)
        {
            var cast = CastBuffer[i];
            cast.Priority = i;
            cast.Layer = this;
            if (cast.Parent == null)
                Children.Add(cast);
        }

        void ParseTree(int idx, int p = -1)
        {
            var node = tree[idx];

            if (node.ChildIndex != -1)
            {
                CastBuffer[idx].Add(CastBuffer[node.ChildIndex]);
                ParseTree(node.ChildIndex, idx);
            }

            if (node.SiblingIndex != -1)
            {
                if (p != -1)
                    CastBuffer[p].Add(CastBuffer[node.SiblingIndex]);
                
                ParseTree(node.SiblingIndex, p);
            }
        }
    }

    public void Write(BinaryObjectWriter writer, Scene context)
    {
        writer.Write(CastBuffer.Count);
        writer.WriteOffset(() =>
        {
            foreach (var cast in CastBuffer)
                writer.WriteObjectOffset(cast, this);
        });
        
        writer.Write<int>(0);
        
        var nodes = new TreeDescriptorNode[CastBuffer.Count];

        // Initialise items because, the runtime doesn't
        for (int i = 0; i < nodes.Length; i++)
            nodes[i] = new TreeDescriptorNode();
        
        BuildTree(Children);

        writer.WriteArrayOffset(nodes);

        void BuildTree(IList<Cast> casts)
        {
            for (int i = 0; i < casts.Count; i++)
            {
                var cast = casts[i];
                var idx = CastBuffer.IndexOf(cast);
                
                if (cast.Children.Count > 0)
                    nodes[idx].ChildIndex = CastBuffer.IndexOf(cast.Children[0]);

                if (i + 1 < casts.Count)
                    nodes[idx].SiblingIndex = CastBuffer.IndexOf(casts[i + 1]);

                BuildTree(cast);
            }
        }
    }

    internal void NotifyCastAdded(Cast cast)
    {
        TakeOwnership(cast);
    }

    internal void NotifyCastRemoved(Cast cast)
    {
        Disown(cast);
    }

    internal void NotifyPriorityChanged(Cast cast)
    {
        CastBuffer.Sort((x, y) => x.Priority - y.Priority);
    }

    private void TakeOwnership(Cast cast)
    {
        cast.Layer?.Disown(cast);
        
        cast.Layer = this;
        CastBuffer.Add(cast);
        foreach (var child in cast)
            TakeOwnership(child);
    }

    private void Disown(Cast cast)
    {
        CastBuffer.Remove(cast);
        foreach (var child in cast.Children)
            Disown(child);
    }

    public IEnumerator<Cast> GetEnumerator()
    {
        return Children.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Children).GetEnumerator();
    }

    public void Add(Cast item)
    {
        Children.Add(item);
        TakeOwnership(item);

        if (HasPriority(item))
            CastBuffer.Sort((x, y) => x.Priority - y.Priority);

        bool HasPriority(Cast cast)
        {
            if (cast.Priority != 0)
                return true;

            var hasPrio = false;
            foreach (var child in cast)
            {
                hasPrio = HasPriority(child);
                if (hasPrio)
                    break;
            }

            return hasPrio;
        }
    }

    public void Clear()
    {
        foreach (var cast in CastBuffer)
            cast.Layer = null;
        
        Children.Clear();
        CastBuffer.Clear();
    }

    public bool Contains(Cast item)
    {
        return Children.Contains(item);
    }

    public void CopyTo(Cast[] array, int arrayIndex)
    {
        Children.CopyTo(array, arrayIndex);
    }

    public bool Remove(Cast item)
    {
        if (item?.Layer != this)
            return false;

        Children.Remove(item);
        Disown(item);
        return true;
    }

    public int IndexOf(Cast item)
    {
        return Children.IndexOf(item);
    }

    public void Insert(int index, Cast item)
    {
        TakeOwnership(item);
        Children.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        Disown(Children[index]);
        Children.RemoveAt(index);
    }

    public Cast this[int index]
    {
        get => Children[index];
        set => Children[index] = value;
    }

    internal struct TreeDescriptorNode
    {
        public int ChildIndex;
        public int SiblingIndex;
        
        public TreeDescriptorNode()
        {
            ChildIndex = -1;
            SiblingIndex = -1;
        }

        public override string ToString()
        {
            return $"{{ ChildIndex: {ChildIndex}, SiblingIndex: {SiblingIndex} }}";
        }
    }
}