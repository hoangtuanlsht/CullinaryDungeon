using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGruzState
{
    void OnEnter(GruzMother boss);
    void OnExecute(GruzMother boss);
    void OnExit(GruzMother boss);

}
