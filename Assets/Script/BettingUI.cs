using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BettingUI : MonoBehaviour
{
    private GamaManager gm;

    private void Awake()
    {
        gm = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GamaManager>();
        var canvas = GameObject.Find("Canvas").transform;

        var foldBtn = canvas.Find("����").GetComponent<UnityEngine.UI.Button>();
        var checkBtn = canvas.Find("üũ").GetComponent<UnityEngine.UI.Button>();
        var callBtn = canvas.Find("��").GetComponent<UnityEngine.UI.Button>();
        var raiseBtn = canvas.Find("������").GetComponent<UnityEngine.UI.Button>();
        var allinBtn = canvas.Find("����").GetComponent<UnityEngine.UI.Button>();

        foldBtn.onClick.RemoveAllListeners();
        checkBtn.onClick.RemoveAllListeners();
        callBtn.onClick.RemoveAllListeners();
        raiseBtn.onClick.RemoveAllListeners();
        allinBtn.onClick.RemoveAllListeners();

        foldBtn.onClick.AddListener(OnClickFold);
        checkBtn.onClick.AddListener(OnClickCheck);
        callBtn.onClick.AddListener(OnClickCall);
        raiseBtn.onClick.AddListener(OnClickRaise);
        allinBtn.onClick.AddListener(OnClickAllin);
    }

    private Player CurrentPlayer()
    {
        if (gm.turnOrder.Count == 0 || gm.currentIndex < 0) return null;
        return gm.turnOrder[gm.currentIndex];
    }

    public void OnClickFold()
    {
        var p = CurrentPlayer();
        if (p == null || !p.isMyTurn) return;   // ����
        p.Fold();
    }

    public void OnClickCheck()
    {
        var p = CurrentPlayer();
        if (p == null || !p.isMyTurn) return;
        p.Check();
    }

    public void OnClickCall()
    {
        var p = CurrentPlayer();
        if (p == null || !p.isMyTurn) return;
        p.Call();
    }

    public void OnClickRaise()
    {
        var p = CurrentPlayer();
        if (p == null || !p.isMyTurn) return;
        // �÷��̾� ���ο� �����̴� ���/���� ������ �����Ƿ� �״�� ����
        p.OnRaiseButtonClicked();
    }

    public void OnClickAllin()
    {
        var p = CurrentPlayer();
        if (p == null || !p.isMyTurn) return;
        p.Allin();
    }
}
