using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Aws.Core
{
    public enum S3FileMode
    {
        Create,
        CreateNew,
        Open,
        OpenOrCreate,
        Truncate
    }

    public enum S3FileAccess
    {
        Read,
        Write
    }
}
