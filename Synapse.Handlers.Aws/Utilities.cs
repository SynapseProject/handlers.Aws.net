using System.IO;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Synapse.Handlers.Aws
{
    public static class Utilities
    {
        public static string TransformXml(string xmlString, string xsltString)
        {
            // Process the XML
            XmlTextReader xmlTextReader = new XmlTextReader( new StringReader( xmlString ) );
            XPathDocument xPathDocument = new XPathDocument( xmlTextReader );

            // Process the XSLT
            XmlTextReader xmlTextReaderXslt = new XmlTextReader( new StringReader( xsltString ) );
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            xslCompiledTransform.Load( xmlTextReaderXslt );

            // Handle the output stream
            StringBuilder stringBuilder = new StringBuilder();
            TextWriter textWriter = new StringWriter( stringBuilder );

            // Do the transform
            xslCompiledTransform.Transform( xPathDocument, null, textWriter );

            // Return formatted string
            // return XDocument.Parse( stringBuilder.ToString() ).ToString();
            // Return unformatted string
            return stringBuilder.ToString();
        }
    }
}
