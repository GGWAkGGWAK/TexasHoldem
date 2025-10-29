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

        var foldBtn = canvas.Find("폴드").GetComponent<UnityEngine.UI.Button>();
        var checkBtn = canvas.Find("체크").GetComponent<UnityEngine.UI.Button>();
        var callBtn = canvas.Find("콜").GetComponent<UnityEngine.UI.Button>();
        var raiseBtn = canvas.Find("레이즈").GetComponent<UnityEngine.UI.Button>();
        var allinBtn = canvas.Find("올인").GetComponent<UnityEngine.UI.Button>();

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
        if (p == null || !p.isMyTurn) return;   // 가드
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
        // 플레이어 내부에 슬라이더 토글/실행 로직이 있으므로 그대로 위임
        p.OnRaiseButtonClicked();
    }

    public void OnClickAllin()
    {
        var p = CurrentPlayer();
        if (p == null || !p.isMyTurn) return;
        p.Allin();
    }
}
