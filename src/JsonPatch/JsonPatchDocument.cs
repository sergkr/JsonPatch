using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JsonPatch.Paths;

namespace JsonPatch
{
    public class JsonPatchDocument<TEntity> : IJsonPatchDocument where TEntity : class, new()
    {

        private List<JsonPatchOperation> _operations = new List<JsonPatchOperation>();

        public List<JsonPatchOperation> Operations { get { return _operations; } }

        public void Add(string path, object value)
        {
            if (!PathHelper.IsPathValid(typeof(TEntity), path))
            {
                throw new JsonPatchParseException(String.Format("The path '{0}' is not valid.", path));
            }

            _operations.Add(new JsonPatchOperation
            {
                Operation = JsonPatchOperationType.add,
                Path = path,
                Value = value
            });
        }

        public void Replace(string path, object value)
        {
            if (!PathHelper.IsPathValid(typeof(TEntity), path))
            {
                throw new JsonPatchParseException(String.Format("The path '{0}' is not valid.", path));
            }

            _operations.Add(new JsonPatchOperation
            {
                Operation = JsonPatchOperationType.replace,
                Path = path,
                Value = value
            });
        }

        public void Remove(string path)
        {
            if (!PathHelper.IsPathValid(typeof(TEntity), path))
            {
                throw new JsonPatchParseException(String.Format("The path '{0}' is not valid.", path));
            }
            

            _operations.Add(new JsonPatchOperation
            {
                Operation = JsonPatchOperationType.remove,
                Path = path
            });
        }

        public void Move(string from, string path)
        {
            if (!PathHelper.IsPathValid(typeof(TEntity), from))
            {
                throw new JsonPatchParseException(String.Format("The path '{0}' is not valid.", from));
            }

            if (!PathHelper.IsPathValid(typeof(TEntity), path))
            {
                throw new JsonPatchParseException(String.Format("The path '{0}' is not valid.", path));
            }

            _operations.Add(new JsonPatchOperation
            {
                Operation = JsonPatchOperationType.move,
                From = from,
                Path = path
            });
        }

        public void ApplyUpdatesTo(TEntity entity)
        {
            foreach (var operation in _operations)
            {
                switch (operation.Operation)
                {
                    case JsonPatchOperationType.remove:
                        PathHelper.SetValueFromPath(typeof(TEntity), operation.Path, entity, null, JsonPatchOperationType.remove);
                        break;
                    case JsonPatchOperationType.replace:
                        PathHelper.SetValueFromPath(typeof(TEntity), operation.Path, entity, operation.Value, JsonPatchOperationType.replace);
                        break;
                    case JsonPatchOperationType.add:
                        PathHelper.SetValueFromPath(typeof(TEntity), operation.Path, entity, operation.Value, JsonPatchOperationType.add);
                        break;
                    case JsonPatchOperationType.move:
                        PathHelper.SetValueFromPath(typeof(TEntity), operation.From, entity, null, JsonPatchOperationType.remove);
                        PathHelper.SetValueFromPath(typeof(TEntity), operation.Path, entity, operation.Value, JsonPatchOperationType.add);
                        break;
                    default:
                        throw new NotImplementedException("Operation not supported: " + operation.Operation);
                }
            }
        }
    }
}
