using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JsonPatch.Tests.Entitys;

namespace JsonPatch.Tests
{
    [TestClass]
    public class JsonPatchDocumentTests
    {

        #region JsonPatch Add Tests

        [TestMethod]
        public void Add_ValidPath_OperationAdded()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Add("Foo", "bar");

            //Assert
            Assert.AreEqual(1, patchDocument.Operations.Count);
            Assert.AreEqual(JsonPatchOperationType.add, patchDocument.Operations.Single().Operation);
        }

        [TestMethod, ExpectedException(typeof(JsonPatchParseException))]
        public void Add_InvalidPath_ThrowsJsonPatchParseException()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Add("FooMissing", "bar");
        }

        #endregion

        #region JsonPatch Remove Tests

        [TestMethod]
        public void Remove_ValidPath_OperationAdded()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Remove("Foo");

            //Assert
            Assert.AreEqual(1, patchDocument.Operations.Count);
            Assert.AreEqual(JsonPatchOperationType.remove, patchDocument.Operations.Single().Operation);
        }

        [TestMethod, ExpectedException(typeof(JsonPatchParseException))]
        public void Remove_InvalidPath_ThrowsJsonPatchParseException()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Remove("FooMissing");
        }

        #endregion

        #region JsonPatch Replace Tests

        [TestMethod]
        public void Replace_ValidPath_OperationAdded()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Replace("Foo", "bar");

            //Assert
            Assert.AreEqual(1, patchDocument.Operations.Count);
            Assert.AreEqual(JsonPatchOperationType.replace, patchDocument.Operations.Single().Operation);
        }

        [TestMethod, ExpectedException(typeof(JsonPatchParseException))]
        public void Replace_InvalidPath_ThrowsJsonPatchParseException()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Replace("FooMissing", "bar");
        }

        #endregion

        #region JsonPatch Move Tests

        [TestMethod]
        public void Move_ValidPaths_OperationAdded()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Move("Foo", "Baz");

            //Assert
            Assert.AreEqual(1, patchDocument.Operations.Count);
            Assert.AreEqual(JsonPatchOperationType.move, patchDocument.Operations.Single().Operation);
        }

        [TestMethod]
        public void Move_ArrayIndexes_OperationAdded()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<ArrayEntity>();

            //Act
            patchDocument.Move("Foo/5", "Foo/2");

            //Assert
            Assert.AreEqual(1, patchDocument.Operations.Count);
            Assert.AreEqual(JsonPatchOperationType.move, patchDocument.Operations.Single().Operation);
        }

        [TestMethod, ExpectedException(typeof(JsonPatchParseException))]
        public void Move_InvalidFromPath_ThrowsJsonPatchParseException()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Move("FooMissing", "Baz");
        }

        [TestMethod, ExpectedException(typeof(JsonPatchParseException))]
        public void Move_InvalidDestinationPath_ThrowsJsonPatchParseException()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();

            //Act
            patchDocument.Move("Foo", "BazMissing");
        }

        [TestMethod, ExpectedException(typeof(JsonPatchParseException))]
        public void Move_IncompatibleTypes_ThrowsJsonPatchParseException()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<ComplexEntity>();

            //Act
            patchDocument.Move("Foo/Foo/5", "/Baz/0");      // Attempt to assign string to an object of type SimpleEntity
        }

        #endregion

        #region JsonPatch ApplyUpdatesTo Tests

        #region Add Operation

        [TestMethod]
        public void ApplyUpdate_AddOperation_EntityUpdated()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();
            var entity = new SimpleEntity();

            //Act
            patchDocument.Add("Foo", "bar");
            patchDocument.ApplyUpdatesTo(entity);

            //Assert
            Assert.AreEqual("bar", entity.Foo);
        }

        #endregion

        #region Remove Operation

        [TestMethod]
        public void ApplyUpdate_RemoveOperation_EntityUpdated()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();
            var entity = new SimpleEntity { Foo = "bar" };

            //Act
            patchDocument.Remove("Foo");
            patchDocument.ApplyUpdatesTo(entity);

            //Assert
            Assert.AreEqual(null, entity.Foo);
        }

        #endregion

        #region Replace Operation

        [TestMethod]
        public void ApplyUpdate_ReplaceOperation_EntityUpdated()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();
            var entity = new SimpleEntity { Foo = "bar" };

            //Act
            patchDocument.Replace("Foo", "baz");
            patchDocument.ApplyUpdatesTo(entity);

            //Assert
            Assert.AreEqual("baz", entity.Foo);
        }

        #endregion

        #region Move Operation

        [TestMethod]
        public void ApplyUpdate_MoveOperation_EntityUpdated()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<SimpleEntity>();
            var entity = new SimpleEntity { Foo = "bar", Baz = "qux"};

            //Act
            patchDocument.Move("Foo", "Baz");
            patchDocument.ApplyUpdatesTo(entity);

            //Assert
            Assert.IsNull(entity.Foo);
            Assert.AreEqual("bar", entity.Baz);
        }

        [TestMethod]
        public void ApplyUpdate_MoveArrayElement_EntityUpdated()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<ArrayEntity>();
            var entity = new ArrayEntity
            {
                Foo = new string[] { "Element One", "Element Two", "Element Three" }
            };

            //Act
            patchDocument.Move("/Foo/2", "/Foo/1");
            patchDocument.ApplyUpdatesTo(entity);

            //Assert
            Assert.AreEqual(3, entity.Foo.Length);
            Assert.AreEqual("Element One", entity.Foo[0]);
            Assert.AreEqual("Element Three", entity.Foo[1]);
            Assert.AreEqual("Element Two", entity.Foo[2]);
        }

        [TestMethod]
        public void ApplyUpdate_MoveFromArrayToProperty_EntityUpdated()
        {
            //Arrange
            var patchDocument = new JsonPatchDocument<ComplexEntity>();
            var entity = new ComplexEntity
            {
                Foo = new ArrayEntity
                {
                    Foo = new string[] { "Foo One", "Foo Two", "Foo Three" }
                },
                Qux = new List<SimpleEntity>
                {
                    new SimpleEntity { Foo = "bar" }
                }
            };

            //Act
            patchDocument.Move("/Foo/Foo/1", "/Qux/0/Foo");
            patchDocument.ApplyUpdatesTo(entity);

            //Assert
            Assert.AreEqual(2, entity.Foo.Foo.Length);
            Assert.AreEqual("Foo One", entity.Foo.Foo[0]);
            Assert.AreEqual("Foo Three", entity.Foo.Foo[1]);
            Assert.AreEqual("Foo Two", entity.Qux[0].Foo);
        }

        #endregion

        #endregion

    }
}
