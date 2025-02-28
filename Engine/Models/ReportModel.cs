using System.Collections.Generic;

namespace Engine.Models;

public record ReportModel(long id, string description, List<CommentModel> comments);
