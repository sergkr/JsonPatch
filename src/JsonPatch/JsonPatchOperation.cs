using System;

namespace JsonPatch
{
    public class JsonPatchOperation
    {
        public JsonPatchOperationType Operation { get; set; }
        public String From { get; set; }
        public String Path { get; set; }
        public object Value { get; set; }
    }
}
