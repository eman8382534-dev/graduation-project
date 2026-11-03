using System.ComponentModel;

namespace GrawiaaApp.API.Models
{
    public enum Category
    {
        [Description("Religion")]
        Religion = 0,

        [Description("Social")]
        Social = 1,

        [Description("Homework")]
        Homework = 2,

        [Description("Helping")]
        Helping = 3,

        [Description("Food")]
        Food = 4,

        [Description("Moving")]
        Moving = 5,

        [Description("Feelings")]
        Feelings = 6
    }
}