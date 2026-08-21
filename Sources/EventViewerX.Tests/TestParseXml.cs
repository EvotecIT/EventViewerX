using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace EventViewerX.Tests {
    public class TestParseXml {
        [Fact]
        public void DataDictionaryIsCaseInsensitive() {
            const string xml = "<Event><EventData><Data Name='Key'>Value</Data></EventData></Event>";
            Dictionary<string, string> result = Parse(xml);
            Assert.Equal("Value", result["Key"]);
            Assert.Equal("Value", result["key"]);
        }

        [Fact]
        public void UserDataReadsFirstPayloadContainerAndNestedText() {
            const string xml = """
                <Event xmlns="urn:event">
                  <UserData>
                    <Audit xmlns="urn:audit">
                      <Field>one<Part>two</Part>three</Field>
                      <Other>Value</Other>
                    </Audit>
                    <Ignored xmlns="urn:ignored">
                      <Field>ignored</Field>
                    </Ignored>
                  </UserData>
                </Event>
                """;

            Dictionary<string, string> result = Parse(xml);

            Assert.Equal("onetwothree", result["Field"]);
            Assert.Equal("Value", result["Other"]);
        }

        [Fact]
        public void ColonSeparatedFieldsDoNotOverwriteNamedData() {
            const string xml = """
                <Event>
                  <EventData>
                    <Data Name="Key">Original</Data>
                    <Data>Key: Embedded
                Extra: Value</Data>
                    <Data />
                  </EventData>
                </Event>
                """;

            Dictionary<string, string> result = Parse(xml);

            Assert.Equal("Original", result["Key"]);
            Assert.Equal("Value", result["Extra"]);
            Assert.Contains("Key: Embedded", result["NoNameA0"]);
            Assert.Contains("Extra: Value", result["NoNameA0"]);
            Assert.DoesNotContain("NoNameA1", result.Keys);
        }

        [Fact]
        public void RepeatedColonSeparatedFieldsKeepTheLastValue() {
            const string xml = """
                <Event>
                  <EventData>
                    <Data>Key: first
                Key: second</Data>
                  </EventData>
                </Event>
                """;

            Dictionary<string, string> result = Parse(xml);

            Assert.Equal("second", result["Key"]);
        }

        private static Dictionary<string, string> Parse(string xml) {
            var obj = (EventObject)FormatterServices.GetUninitializedObject(typeof(EventObject));
            MethodInfo method = typeof(EventObject).GetMethod(
                "ParseXML",
                BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(typeof(Dictionary<string, string>));
            return (Dictionary<string, string>)method.Invoke(obj, new object[] { xml })!;
        }
    }
}
