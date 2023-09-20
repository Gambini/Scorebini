using System;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Scorebini.Data;

namespace ScorebiniTest
{
    [TestClass]
    public class StringOrIntIdTest
    {

        class ObjectContainingId
        {
            public StringOrIntId Id { get; set; }
        }

        class ObjectContainingString
        {
            public string? Id { get; set; }
        }

        class ObjectContainingInt
        {
            public long Id { get; set; }
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("a")]
        [DataRow("0")]
        [DataRow("-1")]
        [DataRow("1")]
        [DataRow("preview_000121_1_2")]
        [DataRow("0x")]
        [DataRow(null)]
        public void StringJsonInputRoundTripTests(string val)
        {
            var orig = new ObjectContainingString { Id = val };
            var origJson = JsonConvert.SerializeObject(orig);
            var fromJson = JsonConvert.DeserializeObject<ObjectContainingId>(origJson);
            Assert.IsTrue(fromJson != null);
            // It is tough to test anything else since there is string->int promotion when parseable
            Assert.IsTrue(new StringOrIntId(val) == fromJson.Id);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(-1)]
        [DataRow(2000)]
        [DataRow(-2000)]
        [DataRow(int.MinValue)]
        [DataRow(int.MaxValue)]
        [DataRow(int.MinValue - 1L)]
        [DataRow(int.MaxValue + 1L)]
        [DataRow(long.MinValue)]
        [DataRow(long.MaxValue)]
        public void IntJsonInputRoundTripTests(long val)
        {
            var orig = new ObjectContainingInt { Id = val };
            var origJson = JsonConvert.SerializeObject(orig);
            var fromJson = JsonConvert.DeserializeObject<ObjectContainingId>(origJson);
            Assert.IsTrue(fromJson != null);
            // It is tough to test anything else since there is string->int promotion when parseable
            Assert.IsTrue(new StringOrIntId(val) == fromJson.Id);
        }


        static IEnumerable<object[]> IntBoundaryStrTestObjData
        {
            get
            {
                foreach (var s in IntBoundaryStrTestRawData)
                {
                    yield return new object[] { s };
                }
            }
        }

        static IEnumerable<string> IntBoundaryStrTestRawData
        {
            get
            {
                yield return 0.ToString();
                yield return 1.ToString();
                yield return (-1).ToString();
                yield return int.MinValue.ToString();
                yield return int.MaxValue.ToString();
                yield return (int.MinValue - 1L).ToString();
                yield return (int.MaxValue + 1L).ToString();
                yield return (long.MaxValue).ToString();
                yield return (long.MinValue).ToString();
            }
        }

        [TestMethod]
        [DynamicData(nameof(IntBoundaryStrTestObjData))]
        public void StringIntPromotionTest(string val)
        {
            var orig = new ObjectContainingString { Id = val };
            var origJson = JsonConvert.SerializeObject(orig);
            var fromJson = JsonConvert.DeserializeObject<ObjectContainingId>(origJson);
            Assert.IsTrue(fromJson != null);
            Assert.IsTrue(new StringOrIntId(val) == fromJson.Id);
            Assert.IsTrue(fromJson.Id.Type == StringOrIntId.IdType.Int);
        }


    }
}
