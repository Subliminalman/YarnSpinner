using System.Collections.Generic;
using Yarn;
using CsvHelper;

namespace Yarn
{

	class TableGenerator
	{
        struct LineData {
            public string text;
            public string lineNum;
            public string character;
        }
		static internal int GenerateTables (GenerateTableOptions options)
		{

			YarnSpinnerConsole.CheckFileList(options.files, YarnSpinnerConsole.ALLOWED_EXTENSIONS);
            YarnSpinnerConsole.Note ("Start");
			if (options.verbose && options.onlyUseTag != null) {
				YarnSpinnerConsole.Note(string.Format("Only using lines from nodes tagged \"{0}\"", options.onlyUseTag));
			}

			bool linesWereUntagged = false;

			foreach (var file in options.files) {                
                var dialogue = YarnSpinnerConsole.CreateDialogueForUtilities();

				dialogue.LoadFile (file);

				var stringTable = dialogue.GetStringTable ();

                var program = dialogue.program;

				var emittedStringTable = new Dictionary<string,string> ();

                var emittedLineTable = new Dictionary<string, LineData> ();

				var anyLinesAreUntagged = false;

                YarnSpinnerConsole.Note ("Options Only use tag is not null: " + (options.onlyUseTag != null));

                string currentCharacter = "";
                Dictionary<string, string> currentOptions = new Dictionary<string, string> ();
                foreach (var n in program.nodes) {
                   
                    foreach (var instruction in n.Value.instructions) {
                        switch (instruction.operation) {
                            case ByteCode.RunLine:                                
                                //Set it up for checkin tags later
                                string line = program.GetString((string)instruction.operandA);                                
                                emittedStringTable[(string)instruction.operandA] = line;
                                LineData lineData;
                                lineData.lineNum = (string)instruction.operandA;
                                lineData.text = line;
                                lineData.character = currentCharacter;
                                emittedLineTable.Add (lineData.lineNum, lineData);
                                YarnSpinnerConsole.Note ("Getting Character Set - Key: " + (string)instruction.operandA + " Value: " + line + " Character: " + lineData.character);                                
                                break;
                            case ByteCode.AddOption:

                                break;
                            case ByteCode.ShowOptions:
                                currentCharacter = "Fennel";
                                foreach (var option in currentOptions) {
                                    LineData ld;
                                    ld.lineNum = option.Key;
                                    ld.text = program.GetString (option.Key);
                                    ld.character = currentCharacter;
                                    emittedLineTable.Add (ld.lineNum, ld);
                                }                                
                                break;
                            case ByteCode.RunCommand:
                                YarnSpinnerConsole.Note ("Going through command: " + (string)instruction.operandA);
                                string currentLine = (string)instruction.operandA;
                                if (currentLine.StartsWith ("Show", System.StringComparison.Ordinal)) {
                                    YarnSpinnerConsole.Note ("Command Starts with Show: " + (string)instruction.operandA);
                                    string[] lines = currentLine.Split (new char[] { ' ' });
                                    if (lines.Length > 2) {
                                        YarnSpinnerConsole.Note ("Lines are longer than 2: " + lines[2]);
                                        string[] c = lines[2].Split (new char[] { '/' });
                                        if (c.Length > 0) {
                                            currentCharacter = c[c.Length - 1];
                                            YarnSpinnerConsole.Note ("Getting Character: " + currentCharacter);
                                        }
                                    }
                                } else if (currentLine.StartsWith ("Hide", System.StringComparison.Ordinal)) {
                                    currentCharacter = "";
                                }
                                break;
                        }
                    }
                }


                foreach (var entry in stringTable) {


					// If options.onlyUseTag is set, we skip all lines in nodes that
					// don't have that tag.
					if (options.onlyUseTag != null) {

						// Find the tags for the node that this string is in
						LineInfo stringInfo;

						try {
							stringInfo = dialogue.program.lineInfo[entry.Key];
						} catch (KeyNotFoundException) {
							YarnSpinnerConsole.Error(string.Format("{0}: lineInfo table does not contain an entry for line {1} (\"{2}\")", file, entry.Key, entry.Value));
							return 1;
						}

						Node node;

						try {
							node = dialogue.program.nodes[stringInfo.nodeName];
						} catch (KeyNotFoundException) {
							YarnSpinnerConsole.Error(string.Format("{0}: Line {1}'s lineInfo claims that the line originates in node {2}, but this node is not present in this program.", file, entry.Key, stringInfo.nodeName));
							return 1;
						}


						var tags = node.tags;

						// If the tags don't include the one we're looking for,
						// skip this line
						if (tags.FindIndex(i => i == options.onlyUseTag) == -1) {                            
							continue;
						}

					}

                    if (entry.Key.StartsWith("line:", System.StringComparison.Ordinal) == false) {
						anyLinesAreUntagged = true;
                        YarnSpinnerConsole.Warn ("Untagged line - Key: " + entry.Key + " Value: " + entry.Value);
					} else {
                        YarnSpinnerConsole.Note ("Entry - Key: " + entry.Key + " Value: " + entry.Value);
                        emittedStringTable [entry.Key] = entry.Value;
					}
				}

				if (anyLinesAreUntagged) {
					YarnSpinnerConsole.Warn(string.Format("Untagged lines in {0}", file));
					linesWereUntagged = true;
				}

				// Generate the CSV

				using (var w = new System.IO.StringWriter()) {
					using (var csv = new CsvWriter(w)) {
                        YarnSpinnerConsole.Note ("StartWriting: " + file);

                        csv.WriteHeader<LocalisedLine>();

						foreach (var entry in emittedStringTable)
						{
							var l = new LocalisedLine();
							l.LineCode = entry.Key;
							l.LineText = entry.Value;
                            if (emittedLineTable.ContainsKey (entry.Key)) {
                                l.Character = emittedLineTable[entry.Key].character;
                            } else {
                                l.Character = "";
                            }
							l.Comment = "";
                            
                            YarnSpinnerConsole.Note ("LocalizedLine - Code: " + l.LineCode + " Text: " + l.LineText + " Character: " + l.Character);
                            csv.WriteRecord(l);
						}

						var dir = System.IO.Path.GetDirectoryName(file);
						var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
						fileName += "_lines.csv";
						var filePath = System.IO.Path.Combine(dir, fileName);

						System.IO.File.WriteAllText(filePath, w.ToString());

						if (options.verbose)
						{
							YarnSpinnerConsole.Note("Wrote " + filePath);
						}
					}					
				}

			}

			if (linesWereUntagged) {
				YarnSpinnerConsole.Warn("Some lines were not tagged, so they weren't added to the " +
				               "string file. Use this tool's 'generate' action to add them.");
			}

			return 0;

		}        

		string CreateCSVRow (params string[] entries) {
			return string.Join (",", entries);
		}

		string CreateCSVRow (KeyValuePair<string,string> entry) {
			return CreateCSVRow (new string[] { entry.Key, entry.Value });
		}
	}

}
