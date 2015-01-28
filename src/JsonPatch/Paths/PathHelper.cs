using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonPatch.Extensions;
using JsonPatch.Helpers;
using Newtonsoft.Json;

namespace JsonPatch.Paths
{
    public class PathHelper
    {
        #region IsPathValid
        
        public static bool IsPathValid(Type entityType, string path)
        {
            try
            {
                ParsePath(path, entityType);
                return true;
            }
            catch (JsonPatchParseException e)
            {
                return false;
            }
        }

        #endregion

        public class PathComponent
        {
            public PathComponent(string name)
            {
                Name = name;
            }

            public string Name { get; set; }
            public Type ComponentType { get; set; }

            public bool IsCollection
            {
                get
                {
                    return typeof (IEnumerable<>).IsAssignableFrom(ComponentType) ||
                           typeof (IEnumerable).IsAssignableFrom(ComponentType);
                }
            }
        }

        public class PropertyPathComponent : PathComponent
        {
            public PropertyPathComponent(string name) : base(name)
            {
            }

            public PropertyInfo PropertyInfo { get; set; }
        }

        public class CollectionIndexPathComponent : PathComponent
        {
            public CollectionIndexPathComponent(string name) : base(name)
            {
            }

            public int CollectionIndex { get; set; }
        }

        public static PathComponent[] ParsePath(string path, Type entityType)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new JsonPatchParseException("Path may not be empty.");
            }

            // Trim any leading and trailing slashes from the path. Modify the path variable itself so that
            // any character positions we report in error messages are accurate.
            path = path.Trim('/');

            // Keep track of our current position in the path string (for error reporting).
            int pos = 0;

            var pathComponents = path.Split('/');
            var parsedComponents = new PathComponent[pathComponents.Length];

            for (int i = 0; i < pathComponents.Length; i++)
            {
                var pathComponent = pathComponents[i];

                try
                {
                    parsedComponents[i] = ParsePathComponent(pathComponent, entityType,
                        i > 0 ? parsedComponents[i - 1] : null);
                }
                catch (JsonPatchParseException e)
                {
                    throw new JsonPatchParseException(string.Format(
                        "The path \"{0}\" is not valid at position {1}. See the inner exception for details.", path, pos), e);
                }

                pos += pathComponent.Length + 1;
            }

            return parsedComponents;
        }

        public static PathComponent ParsePathComponent(string component, Type rootEntityType, PathComponent previous = null)
        {
            if (string.IsNullOrWhiteSpace(component))
            {
                throw new JsonPatchParseException("Path component may not be empty.");
            }

            // If the path component is a positive integer, it represents a collection index.
            if (component.IsPositiveInteger())
            {
                if (previous == null)
                {
                    throw new JsonPatchParseException("The first path component may not be a collection index.");
                }

                if (!previous.IsCollection)
                {
                    throw new JsonPatchParseException(string.Format(
                        "Collection index (\"{0}\") is not valid here because the previous path component (\"{1}\") " +
                        "does not represent a collection type.",
                        component, previous.Name));
                }

                return new CollectionIndexPathComponent(component)
                {
                    CollectionIndex = component.ToInt32(),
                    ComponentType = GetCollectionType(previous.ComponentType)
                };
            }

            // Otherwise, the path component represents a property name.

            // Attempt to retrieve the corresponding property.
            Type parentType = (previous == null) ? rootEntityType : previous.ComponentType;
            var property = parentType.GetProperty(component);

            if (property == null)
            {
                throw new JsonPatchParseException(string.Format("There is no property named \"{0}\" on type {1}.",
                    component, parentType.Name));
            }

            return new PropertyPathComponent(component)
            {
                PropertyInfo = property,
                ComponentType = property.PropertyType
            };
        }

        #region GetPathInfo

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

        #endregion

        #region GetPathInfoWithEntity

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

        #endregion

        #region SetValueFromPath

        /*
        public static void IteratePath(PathComponent[] pathComponents, object entity,
            Action<PropertyPathComponent, object> propertyAction,
            Action<CollectionIndexPathComponent, object> collectionAction)
        {
            object previous = entity;

            foreach (var pathComponent in pathComponents)
            {
                TypeSwitch.On(pathComponent)
                    .Case((PropertyPathComponent component) =>
                    {
                        propertyAction(component, previous);
                        previous = component.PropertyInfo.GetValue(previous);
                    })
                    .Case((CollectionIndexPathComponent component) =>
                    {
                        collectionAction(component, previous);
                        var list = (IList) previous;
                        previous = list[component.CollectionIndex];
                    });
            }
        }
        */

        public static object GetValueFromPath(Type entityType, string path, object entity)
        {
            return GetValueFromPath(entityType, ParsePath(path, entityType), entity);
        }

        public static object GetValueFromPath(Type entityType, IEnumerable<PathComponent> pathComponents, object entity)
        {
            if (entity == null)
            {
                throw new JsonPatchException("Entity is null");
            }

            object previous = entity;

            foreach (var pathComponent in pathComponents)
            {
                TypeSwitch.On(pathComponent)
                    .Case((PropertyPathComponent component) =>
                    {
                        if (previous == null)
                        {
                            throw new JsonPatchException(string.Format("Cannot get property {0} from null.", component.Name));
                        }

                        previous = component.PropertyInfo.GetValue(previous);
                    })
                    .Case((CollectionIndexPathComponent component) =>
                    {
                        if (previous == null)
                        {
                            throw new JsonPatchException(string.Format("Cannot access index {0} from null", component.CollectionIndex));
                        }

                        var list = (IList)previous;
                        previous = list[component.CollectionIndex];
                    });
            }

            return previous;
        }

        public static void SetValueFromPath(Type entityType, string path, object entity, object value, JsonPatchOperationType operationType)
        {
            var pathComponents = ParsePath(path, entityType);
            object previous = GetValueFromPath(entityType, pathComponents.Take(pathComponents.Length - 1), entity);

            if (previous == null)
            {
                throw new JsonPatchException("Previous path component is null");
            }

            var target = pathComponents.Last();

            TypeSwitch.On(target)
                .Case((PropertyPathComponent component) =>
                {
                    switch (operationType)
                    {
                        case JsonPatchOperationType.add:
                            object current = component.PropertyInfo.GetValue(previous);
                            if (current != null)
                            {
                                throw new JsonPatchException("Invalid add: property already has a value.");
                            }
                            component.PropertyInfo.SetValue(previous, ConvertValue(value, component.ComponentType));
                            break;
                        case JsonPatchOperationType.remove:
                            component.PropertyInfo.SetValue(previous, null);
                            break;
                        case JsonPatchOperationType.replace:
                            component.PropertyInfo.SetValue(previous, ConvertValue(value, component.ComponentType));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("operationType");
                    }
                })
                .Case((CollectionIndexPathComponent component) =>
                {
                    var list = previous as IList;
                    if (list == null)
                    {
                        throw new JsonPatchException("Invalid collection");
                    }

                    switch (operationType)
                    {
                        case JsonPatchOperationType.add:
                            list.Insert(component.CollectionIndex, ConvertValue(value, component.ComponentType));
                            break;
                        case JsonPatchOperationType.remove:
                            list.RemoveAt(component.CollectionIndex);
                            break;
                        case JsonPatchOperationType.replace:
                            list[component.CollectionIndex] = ConvertValue(value, component.ComponentType);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("operationType");
                    }
                });
        }

        /*
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
        */

        #endregion

        #region Helpers

        private static Type GetCollectionType(Type entityType)
        {
            return entityType.GetElementType() ?? entityType.GetGenericArguments().First();
        }

        private static object ConvertValue(object value, Type type)
        {
            return JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), type);
        }

        #endregion
    }
}
