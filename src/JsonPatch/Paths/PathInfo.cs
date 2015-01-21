using System;
using System.Collections;
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

    public class PathInfoWithEntity : PathInfo
    {
        public PathInfoWithEntity(string path, PathInfoWithEntity parent, object entity) : base(path, parent)
        {
            Entity = entity;
            Parent = parent;
        }

        public object Entity { get; set; }
        public IList ListEntity { get; set; }
        public new PathInfoWithEntity Parent { get; set; }

        public static PathInfoWithEntity Invalid(string path, PathInfoWithEntity parent, object entity)
        {
            return new PathInfoWithEntity(path, parent, entity) { IsValid = false };
        }
    }
}
