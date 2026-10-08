using UnityEngine;

namespace Funseki.School
{
    // Marks School_Greybox as edited by hand: Build School refuses to rebuild a scene that has it.
    // Remove the component (or use Tools > Funseki > Unfreeze School) to let the builder in again.
    [DisallowMultipleComponent]
    public class SchoolFrozen : MonoBehaviour
    {
        [TextArea] public string note = "Школа правится руками. Build School отключён, пока этот компонент на месте.";
    }
}
