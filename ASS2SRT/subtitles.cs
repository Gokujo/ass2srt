using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

//  Classes got from https://github.com/Haaglar/SubCSharp
//  Thanks to Haaglar

namespace ASS2SRT
{
    public class subtitles
    {
        public Encoding EncodingRead = Encoding.Default;

        /// <summary>
        /// Converts an advanced substation alpha subtitle into the local subtitle format
        /// </summary>
        /// <param name="path">Path to the subtitle to read</param>
        private void ReadASS(string path)
        {
            string subContent = File.ReadAllText(path, EncodingRead);
            subContent = Regex.Replace(subContent, @"\{[^}]*\}", ""); //Remove all additional styling
            using (StringReader assFile = new StringReader(subContent))
            {
                string line = "";
                while ((line = assFile.ReadLine()) != null) //Iterate over string
                {
                    switch (state)
                    {
                        case SState.Empty:
                            if (line.StartsWith("[Events]")) //The row before all dialog
                            {
                                assFile.ReadLine();          //Skip the format
                                state = SState.Iterating;
                            }
                            break;
                        case SState.Iterating:
                            if (line.StartsWith("Dialogue:"))       //As Diaglog starts with this
                            {
                                //Split into 10 Segments
                                //Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
                                //TODO: does it require them all?, or will the magic 10 be replace based on ^
                                string[] splitLine = line.Split(CommaArray, 10, StringSplitOptions.None);
                                DateTime beginTime = DateTime.ParseExact(splitLine[1], "H:mm:ss.ff", CultureInfo.InvariantCulture);
                                DateTime endTime = DateTime.ParseExact(splitLine[2], "H:mm:ss.ff", CultureInfo.InvariantCulture);
                                string text = splitLine[9].Replace("\\N", "\n");//Replace \N with actual newlines
                                subTitleLocal.Add(new SubtitleEntry(beginTime, endTime, text));
                            }
                            break;
                    }
                }
            }
            //Since ass/ssa can be in any order we must do this;
            //It shouldn't mess up already ordered for the next part
            subTitleLocal = subTitleLocal.OrderBy(o => o.startTime).ToList();
            JoinSameStart();
        }

        /// <summary>
        /// Converts the local format to Subrip format
        /// </summary>
        /// <param name="path">Output path for subtitle</param>
        private void WriteSRT(string path)
        {
            string nlSRT = GetNewlineType("\n");
            StringBuilder subExport = new StringBuilder();
            int i = 0;
            foreach (SubtitleEntry entry in subTitleLocal)
            {
                i++;
                string sTime = entry.startTime.ToString("HH:mm:ss,fff");
                string eTime = entry.endTime.ToString("HH:mm:ss,fff");
                subExport.Append(i + nlSRT + sTime + " --> " + eTime + nlSRT + entry.content + nlSRT + nlSRT);
            }
            File.WriteAllText(path, subExport.ToString());
        }

        /// <summary>
        ///Remove dupicale start times and join to one
        ///Assumes the subs are sorted 
        ///Taken from and modified from http://stackoverflow.com/questions/14918668/find-duplicates-and-merge-items-in-a-list  
        /// </summary>
        private void JoinSameStart()
        {
            for (int i = 0; i < subTitleLocal.Count - 1; i++)
            {
                var item = subTitleLocal[i];
                for (int j = i + 1; j < subTitleLocal.Count;)
                {
                    var anotherItem = subTitleLocal[j];
                    if (item.startTime > anotherItem.startTime) break; //No point contiuning as the list is sorted
                    if (item.startTime.Equals(anotherItem.startTime))
                    {
                        //We just join the to and hope that they were in the right order
                        //TODO: check y offset and order by that
                        item.content = item.content + "\n" + anotherItem.content;
                        subTitleLocal.RemoveAt(j);
                    }
                    else
                        j++;
                }
            }
        }

        /// <summary>
        /// Read a subtitle from the specified input path / extension
        /// </summary>
        /// <param name="input">Path to the subtitle</param>
        /// <returns>A boolean representing the success of the operation</returns>
        public bool ReadSubtitle(string input)
        {
            subTitleLocal = new List<SubtitleEntry>();
            string extensionInput = Path.GetExtension(input).ToLower();
            switch (extensionInput) //Read file
            {
                case (".ass"):
                case (".ssa"):
                    ReadASS(input);
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Writes a subtitle to the specified output path / extension
        /// </summary>
        /// <param name="output">Output path location with file extension</param>
        /// <returns>A boolean representing the success of the operation</returns>
        public bool WriteSubtitle(string output)
        {
            string extensionOutput = Path.GetExtension(output).ToLower();

            switch (extensionOutput) //Write to file
            {
               
                case (".srt"):
                    WriteSRT(output);
                    break;
                default:
                    return false;
            }
            return true;
        }

    }
}
