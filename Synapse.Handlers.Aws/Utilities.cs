using System;
using System.Dynamic;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using YamlDotNet.Serialization;

namespace Synapse.Handlers.Aws
{
    public static class Utilities
    {
        public static string TransformXml(string xml, string xslt)
        {
            if ( string.IsNullOrWhiteSpace( xslt ) || string.IsNullOrWhiteSpace( xml ) )
                return xml;

            // Process the XML
            XmlTextReader xmlTextReader = new XmlTextReader( new StringReader( xml ) );
            XPathDocument xPathDocument = new XPathDocument( xmlTextReader );

            // Process the XSLT
            XmlTextReader xmlTextReaderXslt = new XmlTextReader( new StringReader( xslt ) );
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            xslCompiledTransform.Load( xmlTextReaderXslt );

            // Handle the output stream
            StringBuilder stringBuilder = new StringBuilder();
            TextWriter textWriter = new StringWriter( stringBuilder );

            // Do the transform
            xslCompiledTransform.Transform( xPathDocument, null, textWriter );

            // Return unformatted string
            return stringBuilder.ToString();
        }

        public static bool IsValidXml(string value)
        {
            bool isValid = true;
            try
            {
                if ( !string.IsNullOrEmpty( value ) )
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml( value );
                    isValid = true;
                }
            }
            catch ( Exception )
            {
                isValid = false;
            }
            return isValid;
        }

        public static string SerializeTargetFormat(string inputXml, string format)
        {
            string serializedData = "";
            if ( !string.IsNullOrWhiteSpace( inputXml ) )
            {
                if ( string.Equals( format, "json", StringComparison.CurrentCultureIgnoreCase ) )
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml( inputXml );
                    doc.RemoveChild( doc.FirstChild ); // Remove XML declaration
                    serializedData = JsonConvert.SerializeXmlNode( doc );
                }
                else if ( string.Equals( format, "yaml", StringComparison.CurrentCultureIgnoreCase ) )
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml( inputXml );
                    doc.RemoveChild( doc.FirstChild ); // Remove XML declaration
                    serializedData = JsonConvert.SerializeXmlNode( doc );

                    ExpandoObjectConverter expConverter = new ExpandoObjectConverter();
                    dynamic deserializedObject = JsonConvert.DeserializeObject<ExpandoObject>( serializedData, expConverter );

                    Serializer serializer = new Serializer();
                    serializedData = serializer.Serialize( deserializedObject );
                }
                else if ( string.Equals( format, "xml", StringComparison.CurrentCultureIgnoreCase ) )
                {
                    serializedData = inputXml;
                }
            }
            return serializedData;
        }

        public static string SerializeXmlResponse(Ec2Response response)
        {
            string serializedData = "";
            XmlSerializer serializer = new XmlSerializer( response.GetType() );
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                Encoding = new UnicodeEncoding( false, false ), // no BOM in a .NET string
                Indent = false,
                OmitXmlDeclaration = false
            };
            using ( StringWriter textWriter = new StringWriter() )
            {
                using ( XmlWriter xmlWriter = XmlWriter.Create( textWriter, settings ) )
                {
                    XmlSerializerNamespaces ns = new XmlSerializerNamespaces(); // Omit all xsi and xsd namespaces
                    ns.Add( "", "" );
                    serializer.Serialize( xmlWriter, response, ns );
                }
                serializedData = textWriter.ToString();
            }
            return serializedData;
        }


        public static string RemoveParameterSingleQuote(string input)
        {
            string output = "";
            if ( !string.IsNullOrWhiteSpace( input ) )
            {
                Regex pattern = new Regex( "'(\r\n|\r|\n|$)" );
                output = input.Replace( ": '", ": " );
                output = pattern.Replace( output, Environment.NewLine );
            }
            return output;
        }
    }
}

