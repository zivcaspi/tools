using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace xmlsig
{
    // https://stackoverflow.com/questions/43939085/how-to-verify-digital-xml-signature
    internal class Program
    {
        static bool VerifyXmlFile(string name)
        {
            CryptoConfig.AddAlgorithm(typeof(MyXmlDsigC14NTransform), "http://www.w3.org/TR/2001/REC-xml-c14n-20010315");
            var xmlDocument = new XmlDocument();
            xmlDocument.PreserveWhitespace = true;
            xmlDocument.Load(name);
            MyXmlDsigC14NTransform.document = xmlDocument;

            var soapBody = xmlDocument.GetElementsByTagName("SOAP-ENV:Body")[0] as XmlElement;
            var nodes = xmlDocument.GetElementsByTagName("SOAP-ENV:Header");
            var node = nodes.Item(0);
            var securityToken = node.FirstChild.NextSibling.FirstChild.NextSibling.InnerText;

            var signedXml = new SignedXmlWithId(soapBody);

            var nodeList = xmlDocument.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#");
            signedXml.LoadXml(nodeList[0] as XmlElement);

            var byteCert = Convert.FromBase64String(securityToken);
            var cert = new X509Certificate2(byteCert);

            return signedXml.CheckSignature(cert, true);
        }

        static void Main(string[] args)
        {
            var ok = VerifyXmlFile(@"e:\temp\ke.xml");
            Console.WriteLine(ok);
        }
    }

    public class SignedXmlWithId : SignedXml
    {
        public SignedXmlWithId(XmlDocument xml) : base(xml) { }
        public SignedXmlWithId(XmlElement xmlElement) : base(xmlElement) { }

        public override XmlElement GetIdElement(XmlDocument doc, string id)
        {
            XmlElement idElem = base.GetIdElement(doc, id);
            if (idElem == null)
            {
                var nsManager = new XmlNamespaceManager(doc.NameTable);
                nsManager.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
                idElem = doc.SelectSingleNode((Convert.ToString("//*[@wsu:Id=\"") + id) + "\"]", nsManager) as XmlElement;
            }
            return idElem;
        }
    }

    public class MyXmlDsigC14NTransform : XmlDsigC14NTransform
    {
        static XmlDocument _document;
        public static XmlDocument document
        {
            set
            {
                _document = value;
            }
        }

        public MyXmlDsigC14NTransform() { }

        public override Object GetOutput()
        {
            return base.GetOutput();
        }

        public override void LoadInnerXml(XmlNodeList nodeList)
        {
            base.LoadInnerXml(nodeList);
        }

        protected override XmlNodeList GetInnerXml()
        {
            XmlNodeList nodeList = base.GetInnerXml();
            return nodeList;
        }

        public XmlElement GetXml()
        {
            return base.GetXml();
        }

        public override void LoadInput(Object obj)
        {
            int n;
            bool fDefaultNS = true;

            XmlElement element = ((XmlDocument)obj).DocumentElement;

            if (element.Name.Contains("SignedInfo"))
            {
                XmlNodeList DigestValue = element.GetElementsByTagName("DigestValue", element.NamespaceURI);
                string strHash = DigestValue[0].InnerText;
                XmlNodeList nodeList = _document.GetElementsByTagName(element.Name);

                for (n = 0; n < nodeList.Count; n++)
                {
                    XmlNodeList DigestValue2 = ((XmlElement)nodeList[n]).GetElementsByTagName("DigestValue", ((XmlElement)nodeList[n]).NamespaceURI);
                    string strHash2 = DigestValue2[0].InnerText;
                    if (strHash == strHash2) break;
                }

                XmlNode node = nodeList[n];

                while (node.ParentNode != null)
                {
                    XmlAttributeCollection attrColl = node.ParentNode.Attributes;
                    if (attrColl != null)
                    {
                        for (n = 0; n < attrColl.Count; n++)
                        {
                            XmlAttribute attr = attrColl[n];
                            if (attr.Prefix == "xmlns")
                            {
                                element.SetAttribute(attr.Name, attr.Value);
                            }
                            else if (attr.Name == "xmlns")
                            {
                                if (fDefaultNS)
                                {
                                    element.SetAttribute(attr.Name, attr.Value);
                                    fDefaultNS = false;
                                }
                            }
                        }
                    }

                    node = node.ParentNode;
                }
            }

            base.LoadInput(obj);
        }
    }
}
