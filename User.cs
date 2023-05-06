using System.Collections.Generic;

namespace ToDoBot
{
    public class User
    {
        public List<string> TasksList = new List<string>();
        public static string UserID { get; set; }
    }
}
