﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using log4net;
//using Sdl.Utilities.BatchSearchReplace.Lib;

namespace SDLXLIFFSliceOrChange
{
    public static class ReplaceManager
    {
        private static ILog logger = LogManager.GetLogger(typeof (ReplaceManager));
        public static void DoReplaceInFile(string file, ReplaceSettings settings, SDLXLIFFSliceOrChange sdlxliffSliceOrChange)
        {
            try
            {
                sdlxliffSliceOrChange.StepProcess("Replaceing in file: "+file+"...");

                String fileContent = String.Empty;
                using (StreamReader sr = new StreamReader(file))
                {
                    fileContent = sr.ReadToEnd();
                }
                fileContent = Regex.Replace(fileContent, "\t", "");

                using (StreamWriter sw = new StreamWriter(file, false))
                {
                    sw.Write(fileContent);
                }

                XmlDocument xDoc = new XmlDocument();
                xDoc.PreserveWhitespace = true;
                xDoc.Load(file);

                String xmlEncoding = "utf-8";
                try
                {
                    if (xDoc.FirstChild.NodeType == XmlNodeType.XmlDeclaration)
                    {
                        // Get the encoding declaration.
                        XmlDeclaration decl = (XmlDeclaration)xDoc.FirstChild;
                        xmlEncoding = decl.Encoding;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message, ex);
                }

                XmlNodeList fileList = xDoc.DocumentElement.GetElementsByTagName("file");
                foreach (XmlElement fileElement in fileList.OfType<XmlElement>())
                {
                    XmlElement bodyElement = (XmlElement) (fileElement.GetElementsByTagName("body")[0]);
                    XmlNodeList groupElements = bodyElement.GetElementsByTagName("group");

                    foreach (var groupElement in groupElements.OfType<XmlElement>())
                    {
                        //look in segments
                        XmlNodeList transUnits = ((XmlElement) groupElement).GetElementsByTagName("trans-unit");
                        foreach (XmlElement transUnit in transUnits.OfType<XmlElement>())
                        {
                            XmlNodeList source = transUnit.GetElementsByTagName("source");
                            if (source.Count > 0) //in mrk, g si innertext
                                ReplaceAllChildsValue((XmlElement) source[0], settings);
                            source = null;
                            XmlNodeList segSource = transUnit.GetElementsByTagName("seg-source");
                            if (segSource.Count > 0) //in mrk, g si innertext
                                ReplaceAllChildsValue((XmlElement) segSource[0], settings);
                            segSource = null;
                            XmlNodeList target = transUnit.GetElementsByTagName("target");
                            if (target.Count > 0) //in mrk, g si innertext
                                ReplaceAllChildsValue((XmlElement) target[0], settings, false);
                            target = null;
                        }
                    }

                    //look in segments not located in groups
                    XmlNodeList transUnitsInBody = bodyElement.ChildNodes;//.GetElementsByTagName("trans-unit");
                    foreach (XmlElement transUnit in transUnitsInBody.OfType<XmlElement>())
                    {
                        if (transUnit.Name != "trans-unit")
                            continue;
                        XmlNodeList source = transUnit.GetElementsByTagName("source");
                        if (source.Count > 0) //in mrk, g si innertext
                            ReplaceAllChildsValue((XmlElement)source[0], settings);
                        source = null;
                        XmlNodeList segSource = transUnit.GetElementsByTagName("seg-source");
                        if (segSource.Count > 0) //in mrk, g si innertext
                            ReplaceAllChildsValue((XmlElement)segSource[0], settings);
                        segSource = null;
                        XmlNodeList target = transUnit.GetElementsByTagName("target");
                        if (target.Count > 0) //in mrk, g si innertext
                            ReplaceAllChildsValue((XmlElement)target[0], settings, false);
                        target = null;
                    }

                    bodyElement = null;
                    groupElements = null;
                    transUnitsInBody = null;
                }
                Encoding encoding = new UTF8Encoding();
                if (!String.IsNullOrEmpty(xmlEncoding))
                    encoding = Encoding.GetEncoding(xmlEncoding);

                using (var writer = new XmlTextWriter(file, encoding))
                {
                    ////writer.Formatting = Formatting.None;
                    xDoc.Save(writer);
                }

                fileContent = String.Empty;
                using (StreamReader sr = new StreamReader(file))
                {
                    fileContent = sr.ReadToEnd();
                }
                fileContent = Regex.Replace(fileContent, "", "\t");

                using (StreamWriter sw = new StreamWriter(file, false))
                {
                    sw.Write(fileContent);
                }

                xDoc = null;
                fileList = null;
                sdlxliffSliceOrChange.StepProcess("All information replaced in file: " + file + ".");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message, ex);
            }
        }

        private static void ReplaceAllChildsValue(XmlElement target, ReplaceSettings settings, bool inSource = true)
        {
            ReplaceTheVaue(settings, inSource, target);
            foreach (XmlElement child in target.ChildNodes.OfType<XmlElement>())
            {
                if ((child.Name == "mrk" && child.HasAttribute("mtype") && child.Attributes["mtype"].Value != "seg") || child.Name == "g")
                {
                    ReplaceTheVaue(settings, inSource, child);
                }
                ReplaceAllChildsValue(child, settings, inSource);
            }
        }

        private static void ReplaceTheVaue(ReplaceSettings settings, bool inSource, XmlElement child)
        {
            foreach (XmlText childNode in child.ChildNodes.OfType<XmlText>())
            {
                String text = childNode.Value;
                if (settings.UseRegEx)
                {
                    var options = RegexOptions.None;
                    if (!settings.MatchCase)
                        options = RegexOptions.IgnoreCase;
                    childNode.Value = Regex.Replace(text,
                                                    inSource ? settings.SourceSearchText : settings.TargetSearchText,
                                                    inSource ? settings.SourceReplaceText : settings.TargetReplaceText,
                                                    options);
                }
                else
                {
                    string remove = Regex.Escape(inSource ? settings.SourceSearchText : settings.TargetSearchText);
                    string pattern = settings.MatchWholeWord
                                         ? String.Format(@"(\b(?<!\w){0}\b|(?<=^|\s){0}(?=\s|$))", remove)
                                         : remove;
                    string replace = inSource ? settings.SourceReplaceText : settings.TargetReplaceText;

                    childNode.Value = Regex.Replace(text, pattern, replace,
                                                    !settings.MatchCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                }
            }
        }
    }
}