using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public class Commentator
    {
        public string Name { get; set; } = "";
        public string Handle { get; set; } = "";
    }

    public class CommentatorList
    {
        public List<Commentator> Commentators { get; set; } = new();


    }
}
