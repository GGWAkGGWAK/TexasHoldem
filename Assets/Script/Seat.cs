using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Seat : MonoBehaviour
{
    public bool isSeated;
    void Start()
    {
        //isSeated = false;
    }

    void Update()
    {
        // 이 좌석 하위에 Player 컴포넌트가 달린 오브젝트가 존재하면 true
        if (GetComponentInChildren<Player>() != null)
        {
            isSeated = true;
        }
        else
        {
            isSeated = false;
        }
    }

    //서버 연동 시 아래 호출 시스템으로 바꿀 것
    public void OnPlayerEnter()
    {
        isSeated = true;
    }

    public void OnPlayerExit()
    {
        isSeated = false;
    }


}
