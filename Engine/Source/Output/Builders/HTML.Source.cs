﻿/* 
 * Class: GregValure.NaturalDocs.Engine.Output.Builders.HTML
 * ____________________________________________________________________________
 * 
 * 
 * File: Output.nd
 * 
 *		A file used to store information about the last time this output target was built.
 *		
 *		> [String: Style Path]
 *		> [String: Style Path]
 *		> ...
 *		> [String: null]
 *		
 *		Stores the list of styles that apply to this target, in the order in which they must be loaded, as a null-terminated
 *		list of style paths.  The paths are either to <HTMLStyle.CSSFile> or <HTMLStyle.ConfigFile>.  These are stored
 *		instead of the names so that if a name is interpreted differently from one run to the next it will be detected.  It's
 *		also the computed list of styles after all inheritance has been applied.
 * 
 */

// This file is part of Natural Docs, which is Copyright © 2003-2011 Greg Valure.
// Natural Docs is licensed under version 3 of the GNU Affero General Public License (AGPL)
// Refer to License.txt for the complete details


using System;
using System.Collections.Generic;
using System.Text;


namespace GregValure.NaturalDocs.Engine.Output.Builders
	{
	public partial class HTML
		{

		// Group: Types
		// __________________________________________________________________________

		/* Enum: SourcePathType
		 * FolderOnly - The input path doesn't contain a file name.
		 * FileAndFolder - The input path contains a file name and may also contain all or part of a folder path.
		 */
		public enum SourcePathType : byte
			{  FolderOnly, FileAndFolder  };

		/* Enum: OutputPathType
		 * Absolute - Will return an absolute path.
		 * RelativeToRootOutputFolder - Will return a path relative to <RootOutputFolder>.
		 */
		public enum OutputPathType : byte
			{  Absolute, RelativeToRootOutputFolder  };



		// Group: Functions
		// __________________________________________________________________________
		
		/* Function: BuildSourceFile
		 * Builds an output file based on a source file.  The accessor should NOT hold a lock on the database.
		 */
		protected void BuildSourceFile (int fileID, CodeDB.Accessor accessor, CancelDelegate cancelDelegate)
			{
			accessor.GetReadOnlyLock();
			bool haveDBLock = true;

			try
				{
				IList<Topic> topics = accessor.GetTopicsInFile(fileID, cancelDelegate);
				
				if (cancelDelegate())
					{  return;  }
					
				if (topics.Count == 0)
					{
					accessor.ReleaseLock();
					haveDBLock = false;
					
					Path outputFile = Source_OutputFile(fileID);

					if (outputFile != null && System.IO.File.Exists(outputFile))
						{  
						System.IO.File.Delete(outputFile);
						foldersToCheckForDeletion.Add(outputFile.ParentFolder);
						}

					lock (writeLock)
						{
						if (sourceFilesWithContent.Remove(fileID) == true)
							{  buildFlags |= BuildFlags.FileHierarchy;  }
						}
					}

				else // (topics.Count != 0)
					{
					StringBuilder content = new StringBuilder();
					content.Append("<ul>");
						
					foreach (Topic topic in topics)
						{
						content.Append("<li>" + topic.Title + "</li>");
						}
							
					content.Append("</ul>");

					accessor.ReleaseLock();
					haveDBLock = false;

					Path outputPath = Source_OutputFile(fileID);
					BuildFile(outputPath, "test", content.ToString(), PageType.Content);

					lock (writeLock)
						{
						if (sourceFilesWithContent.Add(fileID) == true)
							{  buildFlags |= BuildFlags.FileHierarchy;  }
						}
					}
				}
				
			finally
				{ 
				if (haveDBLock)
					{  accessor.ReleaseLock();  }
				}
			}



		// Group: Path Functions
		// __________________________________________________________________________


		/* Function: Source_OutputFolder
		 * Returns the output folder of the passed file source number and, if specified, the folder within it.  If the folder is null
		 * it returns the root output folder for the file source number.
		 */
		public Path Source_OutputFolder (int number, Path relativeFolder = default(Path))
			{
			StringBuilder result = new StringBuilder(OutputFolder);
			result.Append("/files");  

			if (number != 1)
				{  result.Append(number);  }
					
			if (relativeFolder != null)
				{
				result.Append('/');
				result.Append(SanitizePath(relativeFolder));
				}

			return result.ToString();
			}


		/* Function: Source_OutputFileNameOnly
		 * Returns the output file name of the passed file.  Any path attached to it will be ignored and not included in the result.
		 */
		public static Path Source_OutputFileNameOnly (Path filename)
			{
			string nameString = filename.NameWithoutPath.ToString();

			// We can't have dots in the file name because Apache will try to execute Script.pl.html even though .pl is not
			// the last extension.  Dots in folder names are okay though.
			nameString = nameString.Replace('.', '-');
			
			nameString = SanitizePathString(nameString);
			return nameString + ".html";
			}


		/* Function: Source_OutputFile
		 * Returns the output path of the passed source file ID, or null if none.  It may be null if the <FileSource> that created
		 * it no longer exists.
		 */
		public Path Source_OutputFile (int fileID)
			{
			Files.File file = Engine.Instance.Files.FromID(fileID);
			Files.FileSource fileSource = Engine.Instance.Files.FileSourceOf(file);

			if (fileSource == null)
				{  return null;  }

			Path relativePath = fileSource.MakeRelative(file.FileName);

			return Source_OutputFolder(fileSource.Number, relativePath.ParentFolder) + '/' + 
						 Source_OutputFileNameOnly(relativePath.NameWithoutPath);
			}



		// Group: CodeDB.IChangeWatcher Functions
		// __________________________________________________________________________
		
		
		override public void OnAddTopic (Topic topic, CodeDB.EventAccessor eventAccessor)
			{
			lock (writeLock)
				{
				sourceFilesToRebuild.Add(topic.FileID);
				}
			}

		override public void OnUpdateTopic (Topic oldTopic, int newCommentLineNumber, int newCodeLineNumber, string newBody, 
															CodeDB.EventAccessor eventAccessor)
			{
			lock (writeLock)
				{
				sourceFilesToRebuild.Add(oldTopic.FileID);
				}
			}

		override public void OnDeleteTopic (Topic topic, CodeDB.EventAccessor eventAccessor)
			{
			lock (writeLock)
				{
				sourceFilesToRebuild.Add(topic.FileID);
				}
			}

		}
	}

