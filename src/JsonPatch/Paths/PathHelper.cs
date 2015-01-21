using System;
using System.Collections;
using System.Linq;
using JsonPatch.Extensions;
using Newtonsoft.Json;

namespace JsonPatch.Paths
{
    public class PathHelper
    {
        public static PathInfo GetPathInfo(Type entityType, string path)
        {
            return GetPathInfo(entityType, null, path);
        }

        private static PathInfo GetPathInfo(Type entityType, PathInfo parent, string path)
        {
            if (String.IsNullOrEmpty(path))
                return PathInfo.Invalid(path, parent);

            string[] pathComponents = path.Trim('/').Split('/');

            string currentPath = (parent == null ? "" : parent.Path) + "/" + pathComponents[0];
            var currentPathInfo = new PathInfo(currentPath, parent);

            if (String.IsNullOrEmpty(pathComponents[0]))
                return PathInfo.Invalid(currentPath, parent);

            var currentPathComponent = pathComponents[0];

            if (currentPathComponent.IsPositiveInteger())
            {
                if (!typeof(IEnumerable).IsAssignableFrom(entityType))
                    return PathInfo.Invalid(currentPath, parent);

                currentPathInfo.IsValid = true;
                currentPathInfo.IsCollectionElement = true;
                currentPathInfo.CollectionIndex = currentPathComponent.ToInt32();
                currentPathInfo.Type = GetCollectionType(entityType);

                if (pathComponents.Length == 1)
                {
                    return currentPathInfo;
                }

                return GetPathInfo(GetCollectionType(entityType), parent, String.Join("/", pathComponents.Skip(1)));
            }

            var property = entityType.GetProperties().FirstOrDefault(p => p.Name == pathComponents[0]);
            if (property == null)
                return PathInfo.Invalid(currentPath, parent);

            currentPathInfo.IsValid = true;
            currentPathInfo.IsCollectionElement = false;
            currentPathInfo.Property = property;
            currentPathInfo.Type = property.PropertyType;

            if (pathComponents.Length == 1)
            {
                return currentPathInfo;
            }

            return GetPathInfo(property.PropertyType, currentPathInfo, String.Join("/", pathComponents.Skip(1)));
        }

        private static PathInfoWithEntity GetPathInfoWithEntity(Type entityType, object entity, string path)
        {
            return GetPathInfoWithEntity(entityType, null, entity, path);
        }

        private static PathInfoWithEntity GetPathInfoWithEntity(Type entityType, PathInfoWithEntity parent, object current, string path)
        {
            if (String.IsNullOrEmpty(path))
                return PathInfoWithEntity.Invalid(path, parent, current);

            string[] pathComponents = path.Trim('/').Split('/');
            
            string currentPath = (parent == null ? "" : parent.Path) + "/" + pathComponents[0];
            var currentPathInfo = new PathInfoWithEntity(currentPath, parent, current);

            if (String.IsNullOrEmpty(pathComponents[0]))
                return PathInfoWithEntity.Invalid(currentPath, parent, current);

            var currentPathComponent = pathComponents[0];

            if (currentPathComponent.IsPositiveInteger())
            {
                var listEntity = (IList)current;
                var accessIndex = currentPathComponent.ToInt32();

                if (!typeof(IEnumerable).IsAssignableFrom(entityType))
                    return PathInfoWithEntity.Invalid(currentPath, parent, current);

                currentPathInfo.IsValid = true;
                currentPathInfo.IsCollectionElement = true;
                currentPathInfo.CollectionIndex = currentPathComponent.ToInt32();
                currentPathInfo.ListEntity = listEntity;
                currentPathInfo.Type = GetCollectionType(entityType);
                currentPathInfo.Parent = parent;

                if (pathComponents.Length == 1)
                {
                    return currentPathInfo;
                }

                return GetPathInfoWithEntity(GetCollectionType(entityType), currentPathInfo, listEntity[accessIndex], String.Join("/", pathComponents.Skip(1)));
            }

            var property = entityType.GetProperties().FirstOrDefault(p => p.Name == pathComponents[0]);
            if (property == null)
                return PathInfoWithEntity.Invalid(currentPath, parent, current);

            currentPathInfo.IsValid = true;
            currentPathInfo.IsCollectionElement = false;
            currentPathInfo.Property = property;
            currentPathInfo.Entity = current;
            currentPathInfo.Type = property.PropertyType;

            if (pathComponents.Length == 1)
            {
                return currentPathInfo;
            }

            //If we're still traversing the path, make sure we've instantiated objects along the way
            var propertyValue = property.GetValue(current);
            var propertyType = property.PropertyType;
            if (propertyValue == null)
            {
                if (property.PropertyType.IsArray)
                {
                    propertyValue = Activator.CreateInstance(property.PropertyType, new object[] { 0 });
                }
                else
                {
                    propertyValue = Activator.CreateInstance(property.PropertyType);
                }
                property.SetValue(current, propertyValue);
            }

            return GetPathInfoWithEntity(propertyType, currentPathInfo, propertyValue, String.Join("/", pathComponents.Skip(1)));
        }

        private static Type GetCollectionType(Type entityType)
        {
            return entityType.GetElementType() ?? entityType.GetGenericArguments().First();
        }

        public static bool IsPathValid(Type entityType, string path)
        {
            return GetPathInfo(entityType, path).IsValid;
        }

        public static void SetValueFromPath(Type entityType, string path, object entity, object value, JsonPatchOperationType operationType)
        {
            var pathInfo = GetPathInfoWithEntity(entityType, entity, path);

            if (!pathInfo.IsValid)
                throw new JsonPatchException(String.Format("The path specified ('{0}') is invalid", path));

            if (pathInfo.IsCollectionElement)
            {
                var listEntity = pathInfo.ListEntity;
                var numberOfElements = (listEntity).Count;
                var accessIndex = pathInfo.CollectionIndex;

                if (operationType == JsonPatchOperationType.add)
                    numberOfElements++; //We can add to the end of an array

                if (accessIndex >= numberOfElements)
                {
                    throw new JsonPatchException(String.Format(
                        "The path specified ('{0}') is invalid: The collection has '{1}' elements, attempted to {2} at {3}",
                        path,
                        numberOfElements,
                        operationType,
                        accessIndex));
                }

                // Try set the value in the array
                if (operationType == JsonPatchOperationType.add)
                {
                    // If this isn't an array, we can just insert it and return.
                    if (listEntity is Array == false)
                    {
                        listEntity.Insert(accessIndex, JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), listEntity.GetType().GetGenericArguments().First()));
                        return;
                    }

                    var oldArray = listEntity;
                    IList newArray = (IList)Activator.CreateInstance(listEntity.GetType(), new object[] { (oldArray).Count + 1 });

                    for (int i = 0; i < newArray.Count; i++)
                    {
                        if (i < accessIndex)
                            newArray[i] = oldArray[i];
                        if (i == accessIndex)
                            newArray[i] = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), pathInfo.Type);
                        if (i > accessIndex)
                            newArray[i] = oldArray[i - 1];
                    }

                    if (pathInfo.Parent.IsCollectionElement)
                    {
                        pathInfo.Parent.ListEntity[pathInfo.Parent.CollectionIndex] = newArray;
                    }
                    else
                    {
                        pathInfo.Parent.Property.SetValue(pathInfo.Parent.Entity, newArray);
                    }

                    return;
                }

                if (operationType == JsonPatchOperationType.remove)
                {
                    //If this isn't an array, we can just remove at and return. 
                    if (listEntity is Array == false)
                    {
                        listEntity.RemoveAt(accessIndex);
                        return;
                    }

                    var oldArray = listEntity;
                    var newArray = (IList)Activator.CreateInstance(listEntity.GetType(), new object[] { (oldArray).Count - 1 });

                    for (int i = 0; i < oldArray.Count; i++)
                    {
                        if (i < accessIndex)
                            newArray[i] = oldArray[i];
                        if (i > accessIndex)
                            newArray[i - 1] = oldArray[i];
                    }

                    if (pathInfo.Parent.IsCollectionElement)
                    {
                        pathInfo.Parent.ListEntity[pathInfo.Parent.CollectionIndex] = newArray;
                    }
                    else
                    {
                        pathInfo.Parent.Property.SetValue(pathInfo.Parent.Entity, newArray);
                    }

                    return;
                }

                listEntity[accessIndex] = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), GetCollectionType(listEntity.GetType()));
                return;
            }

            if (operationType == JsonPatchOperationType.add && pathInfo.Property.GetValue(pathInfo.Entity) != null)
            {
                throw new JsonPatchException(string.Format(
                    "Invalid add operation on path \"{0}\": The path already has a value.",
                    path));
            }

            pathInfo.Property.SetValue(pathInfo.Entity, JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), pathInfo.Property.PropertyType));
            return;
        }
    }
}
