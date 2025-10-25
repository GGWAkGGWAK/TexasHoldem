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
        // �� �¼� ������ Player ������Ʈ�� �޸� ������Ʈ�� �����ϸ� true
        if (GetComponentInChildren<Player>() != null)
        {
            isSeated = true;
        }
        else
        {
            isSeated = false;
        }
    }

    //���� ���� �� �Ʒ� ȣ�� �ý������� �ٲ� ��
    public void OnPlayerEnter()
    {
        isSeated = true;
    }

    public void OnPlayerExit()
    {
        isSeated = false;
    }


}
