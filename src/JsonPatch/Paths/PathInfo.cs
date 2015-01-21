using System;
using System.Reflection;

namespace JsonPatch.Paths
{
    public class PathInfo
    {
        public PathInfo(string path, PathInfo parent)
        {
            Path = path;
            Parent = parent;
        }

        public string Path { get; set; }
        public bool IsValid { get; set; }
        public Type Type { get; set; }
        public PropertyInfo Property { get; set; }
        public bool IsCollectionElement { get; set; }
        public int CollectionIndex { get; set; }
        public PathInfo Parent { get; set; }

        public static PathInfo Invalid(string path, PathInfo parent)
        {
            return new PathInfo(path, parent) { IsValid = false };
        }
    }
}
