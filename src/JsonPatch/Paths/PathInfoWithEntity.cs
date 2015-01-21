using System.Collections;

namespace JsonPatch.Paths
{
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
