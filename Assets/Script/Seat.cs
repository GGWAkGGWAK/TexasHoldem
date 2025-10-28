using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Seat : MonoBehaviour
{
    public bool isSeated;

    private void Awake()
    {
        RefreshIsSeated();
    }
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

    private void OnTransformChildrenChanged() // �÷��̾ �ٰų� ������ �� �ڵ� ����
    {
        RefreshIsSeated();
    }
    private void RefreshIsSeated()
    {
        isSeated = GetComponentInChildren<Player>(true) != null;
    }

}
