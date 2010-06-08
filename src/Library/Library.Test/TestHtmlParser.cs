﻿#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.Collections.Generic;
using NUnit.Framework;
using CSharpTest.Net.Html;
using System.IO;
using System.Text.RegularExpressions;
using CSharpTest.Net.Utils;
using System.Net;
using System.Xml.XPath;
using CSharpTest.Net.IO;
using XmlDocument = System.Xml.XmlDocument;

namespace CSharpTest.Net.Library.Test
{
	[TestFixture]
	public partial class TestHtmlParser
	{
		const string document = @"
<?xml version='1.0'?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html>
<head>
    <title>Document Title</title>
    <link href=""style.css"" > </link>
</head><!-- comments included -->
<body id=one class='cls'><![CDATA[ this 
			is > cdata! ]]>
    <div id='two'>Hi</div>,</div>
<tr><td><p class=1><p class=2><b><td><p><i></table>
	this is content.
";

		private string Normalize(string text)
		{
			text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
			while (text.IndexOf("  ") >= 0)
				text = text.Replace("  ", " ");
			return text;
		}

		[Test]
		public void TestDocToXml()
		{
			HtmlLightDocument doc = new HtmlLightDocument();
			new XmlLightElement(new XmlLightElement(doc, "html"), "body").Attributes.Add("id", "bdy");
			Assert.AreEqual("<html> <body id=\"bdy\"></body> </html>", Normalize(doc.InnerXml));
		}

		[Test]
		public void TestParseDocument()
		{
			XmlLightDocument doc = new HtmlLightDocument(document);
			XmlLightDocument doc2;
			using (TempFile t = new TempFile())
			{
				using (TextWriter tw = new StreamWriter(t.Open()))
					doc.WriteXml(tw);
				new XhtmlValidation(XhtmlDTDSpecification.XhtmlTransitional_10).Validate(t.TempPath);
				doc2 = new XmlLightDocument(t.ReadAllText());

				Assert.AreEqual(doc.InnerXml, doc2.InnerXml);
			}
		}

		[Test]
		public void TestParseAttributes()
		{
			IEnumerator<XmlLightAttribute> en;
			en = XmlLightParser.ParseAttributes("<tag a=\"1\" b='2' c=3 d e=>").GetEnumerator();
			Assert.IsTrue(en.MoveNext());
			Assert.AreEqual("a", en.Current.Name);
			Assert.AreEqual("1", en.Current.Value);
			Assert.IsTrue(en.MoveNext());
			Assert.AreEqual("b", en.Current.Name);
			Assert.AreEqual("2", en.Current.Value);
			Assert.IsTrue(en.MoveNext());
			Assert.AreEqual("c", en.Current.Name);
			Assert.AreEqual("3", en.Current.Value);
			Assert.IsTrue(en.MoveNext());
			Assert.AreEqual("d", en.Current.Name);
			Assert.AreEqual(null, en.Current.Value);
			Assert.IsTrue(en.MoveNext());
			Assert.AreEqual("e", en.Current.Name);
			Assert.AreEqual("", en.Current.Value);
			Assert.IsFalse(en.MoveNext());

			en = XmlLightParser.ParseAttributes("<?xml version='1.0'?>").GetEnumerator();
			Assert.IsTrue(en.MoveNext());
			Assert.AreEqual("version", en.Current.Name);
			Assert.AreEqual("1.0", en.Current.Value);
			Assert.IsFalse(en.MoveNext());
		}

		[Test]
		public void TestParseText()
		{
			string text = Normalize(XmlLightParser.ParseText(document));
			Assert.AreEqual("Document Title this is > cdata! Hi, this is content.", text);
		}

		[Test]
		public void TestXPath()
		{
			XmlDocument xdoc = new XmlDocument();
			XmlLightDocument doc = new HtmlLightDocument(document);
			string testpath = "/html/body[@id='one' and @class='cls']/../body/div[@id='two' and text() = 'Hi']/@id";

			xdoc.LoadXml(doc.CreateNavigator().InnerXml);
			Assert.IsNotNull(xdoc.SelectSingleNode(testpath));
			XPathNavigator nav = doc.CreateNavigator().SelectSingleNode(testpath);

			Assert.IsNotNull(nav);
			Assert.IsTrue(nav.NodeType == XPathNodeType.Attribute);
			Assert.AreEqual("id", nav.Name);
			Assert.AreEqual("two", nav.Value);

			XmlLightElement e = doc.SelectSingleNode("/html/Head");
			Assert.IsNull(e);
			e = doc.SelectSingleNode("/html/head");
			Assert.IsNotNull(e);
		}

		[Test]
		public void TestXmlElement()
		{
			XmlLightDocument doc = new HtmlLightDocument(document);
			Assert.IsNull(doc.PrevSibling);
			Assert.IsNull(doc.Children[0].PrevSibling);
			Assert.IsNull(doc.NextSibling);
			Assert.IsNull(doc.Children[doc.Children.Count - 1].NextSibling);

			XmlLightElement e = doc.SelectSingleNode("/html/body//*[@class='2']");
			Assert.IsNotNull(e);
			Assert.AreEqual("p", e.TagName);
			Assert.IsNotNull(e.PrevSibling);
			Assert.AreEqual("p", e.PrevSibling.TagName);

			Assert.AreEqual("", e.Namespace);
			Assert.AreEqual("p", e.LocalName);

			e = new XmlLightElement(null, "a:b");
			Assert.AreEqual("a", e.Namespace);
			Assert.AreEqual("b", e.LocalName);
		}

		[Test]
		public void TestXmlNavigator()
		{
			XPathNavigator nav = new HtmlLightDocument(document).CreateNavigator().SelectSingleNode("/html/body//p[@class='1']");
			XPathNavigator pos = nav.Clone();
			Assert.IsFalse(nav.MoveToPrevious());
			Assert.IsTrue(nav.MoveToNext());
			Assert.IsTrue(nav.MoveToPrevious());
			Assert.IsTrue(nav.IsSamePosition(pos));

			Assert.IsFalse(nav.MoveToFirstNamespace());
			Assert.IsFalse(nav.MoveToNextNamespace());

			Assert.IsTrue(Object.ReferenceEquals(nav.NameTable, pos.NameTable));
			Assert.IsNotNull(nav.BaseURI);
			Assert.AreEqual(nav.BaseURI, pos.BaseURI);

			Assert.IsTrue(nav.MoveToId("one"));
			Assert.AreEqual("body", nav.Name);
			Assert.IsFalse(nav.MoveToId("none-exists"));
			Assert.AreEqual("body", nav.Name);
		}

		[Test]
		public void TestInnerText()
		{
			XmlLightDocument doc = new HtmlLightDocument(document);
			XmlLightElement e = doc.SelectSingleNode("/html/body");
			Assert.AreEqual("this is > cdata! Hi, this is content.", Normalize(e.InnerText));
			Assert.AreEqual("Hi", e.SelectSingleNode(".//div[@id='two']").InnerText);
			Assert.AreEqual("this is > cdata!", Normalize(e.SelectSingleNode("text()").InnerText));
		}

		[Test]
		public void TestComments()
		{
			XmlLightDocument doc = new HtmlLightDocument(document);
			XmlLightElement e = doc.SelectSingleNode("/html/head");
			e = e.NextSibling;
			Assert.IsTrue(e.IsComment);
			Assert.AreEqual("<!-- comments included -->", e.InnerXml);
		}

        [Test]
        public void TestParsers()
        {
            string notxml = "<html id=a ><body foo='bar' bar=\"foo\" />";

            HtmlLightDocument html = new HtmlLightDocument();
            XmlLightParser.Parse(notxml, html);
            Assert.AreEqual("html", html.Root.TagName);
            Assert.AreEqual(1, html.Root.Attributes.Count);
            Assert.AreEqual("a", html.Root.Attributes["id"]);
            Assert.AreEqual(1, html.Root.Children.Count);
            Assert.AreEqual("body", html.Root.Children[0].TagName);
            Assert.AreEqual("foo", html.Root.Children[0].Attributes["bar"]);
            Assert.AreEqual("bar", html.Root.Children[0].Attributes["foo"]);

            XmlLightDocument xml = new XmlLightDocument();
            XmlLightParser.Parse(notxml, XmlLightParser.AttributeFormat.Xml, xml);
            Assert.AreEqual(2, xml.Root.Attributes.Count);
            //Not recognized: xml.Root.Attributes["id"]
            Assert.AreEqual("body", xml.Root.TagName);
            Assert.AreEqual("foo", xml.Root.Attributes["bar"]);
            Assert.AreEqual("bar", xml.Root.Attributes["foo"]);
        }

		[Test, Explicit]
		public void RunPerfTests()
		{
			string[] files = Directory.GetFiles(@"c:\temp\trash", "*.htm", SearchOption.AllDirectories);
			System.Diagnostics.Stopwatch sw;

			for (int i = 0; i < 10; i++)
			{
				//HTML Parser
				sw = new System.Diagnostics.Stopwatch();
				sw.Start();

				foreach (string file in files)
					new HtmlLightDocument(File.ReadAllText(file));

				Console.WriteLine("HTML = {0}", sw.ElapsedMilliseconds);
				//XML Parser
				sw = new System.Diagnostics.Stopwatch();
				sw.Start();

				foreach (string file in files)
					new XmlLightDocument(File.ReadAllText(file));

				Console.WriteLine("XHTM = {0}", sw.ElapsedMilliseconds);
				//Parse Only
				sw = new System.Diagnostics.Stopwatch();
				sw.Start();

				IXmlLightReader rdr = new EmptyReader();
				foreach (string file in files)
					XmlLightParser.Parse(File.ReadAllText(file), XmlLightParser.AttributeFormat.Xml, rdr);

				Console.WriteLine("NDOM = {0}", sw.ElapsedMilliseconds);
				//Text Only
				sw = new System.Diagnostics.Stopwatch();
				sw.Start();

				foreach (string file in files)
					XmlLightParser.ParseText(File.ReadAllText(file));

				Console.WriteLine("TEXT = {0}", sw.ElapsedMilliseconds);
			}
		}

		class EmptyReader : IXmlLightReader
		{
			public void AddCData(string cdata) { }
			public void AddComment(string comment) { }
			public void AddControl(string cdata) { }
			public void AddInstruction(string instruction) { }
			public void AddText(string content) { }
			public void EndDocument() { }
			public void EndTag(string fullName) { }
			public void StartDocument() { }
			public void StartTag(string fullName, bool selfClosed, string unparsedTag, IEnumerable<XmlLightAttribute> attributes) { }
		}

        [Test, ExpectedException(typeof(System.Xml.XmlException))]
        public void TestXmlNoRootNode()
        {
            new XmlLightDocument("no xml root node defined");
        }

        [Test, ExpectedException(typeof(System.Xml.XmlException))]
        public void TestXmlNoClosingTag()
        {
            new XmlLightDocument("<xml tag-not-closed='true'>");
        }

        [Test, ExpectedException(typeof(System.Xml.XmlException))]
        public void TestXmlWrongClosingTag()
        {
            new XmlLightDocument("<xml wrong-tag-closed='true'></other>");
        }

        [Test, ExpectedException(typeof(System.ApplicationException))]
        public void TestRootNodeNotHtml()
        {
            new HtmlLightDocument("<not-html></not-html>");
        }
	}
}