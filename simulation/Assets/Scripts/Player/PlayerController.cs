﻿using System.Collections.Generic;
using Fire;
using GameMenu;
using Mapbox.Unity.Map;
using Player.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        private Vector3 _anchorPoint;
        private Quaternion _anchorRot;
        private bool _mousePressed;
        private List<FireBehaviour> _allFires = new List<FireBehaviour>();
        private bool _allFiresPaused;
        private bool _gamePaused;

        [SerializeField] 
        public PlayerSettings settings = new PlayerSettings();
        public IUnityService UnityService;
        public Hud hudMenu;
        public Settings settingsMenu;
        public new Camera camera;
        public AbstractMap map;
        
        private void Awake()
        {
            _mousePressed = false;
            camera = GetComponent<Camera>();
            UnityService = new UnityService();
        }

        // Update is called once per frame
        private void Update()
        {
            HandleLeftMouseButton();
            HandleRightMouseButton();
            HandleFireIgnition();

            transform.Translate(CalculateMovement());
        }
        
        private void HandleLeftMouseButton()
        {
            if (settingsMenu.IsOpen()) return;
            
            if (UnityService.GetMouseButtonDown(0))
            {
                _mousePressed = true;
            }
            else if (UnityService.GetMouseButtonUp(0))
            {
                _mousePressed = false;
            }
        }

        private Vector3 CalculateMovement()
        {
            if (settingsMenu.IsOpen()) return Vector3.zero;
            
            var move = Vector3.zero;
            if (UnityService.GetKey(KeyCode.W))
                move += Vector3.forward * settings.Speed;
            if (UnityService.GetKey(KeyCode.S))
                move += Vector3.back * settings.Speed;
            if (UnityService.GetKey(KeyCode.D))
                move += Vector3.right * settings.Speed;
            if (UnityService.GetKey(KeyCode.A))
                move += Vector3.left * settings.Speed;
            if (UnityService.GetKey(KeyCode.E))
                move += Vector3.up * settings.Speed;
            if (UnityService.GetKey(KeyCode.Q))
                move += Vector3.down * settings.Speed;

            return move;
        }

        private void HandleRightMouseButton()
        {
            if (settingsMenu.IsOpen()) return;
            
            if (UnityService.GetMouseButtonDown(1))
            {
                _anchorPoint = new Vector3(Input.mousePosition.y, -Input.mousePosition.x);
                _anchorRot = transform.rotation;
            }

            if (UnityService.GetMouseButton(1))
            {
                var rot = _anchorRot;
                var dif = _anchorPoint - new Vector3(Input.mousePosition.y, -Input.mousePosition.x);
                rot.eulerAngles += dif * settings.Sensitivity;
                transform.rotation = rot;
            }
            
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void HandleFireIgnition()
        {
            if (!CanIgnite() || EventSystem.current.IsPointerOverGameObject()) return;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
                
            if (!Physics.Raycast(ray, out var hitInfo, Mathf.Infinity)) return;
                
            CreateFire(hitInfo.point);
        }

        private bool CanIgnite()
        {
            return _mousePressed && hudMenu.IgniteClicked;
        }
        
        public void PauseAllFires(bool pause)
        {
            if (_allFiresPaused == pause) return;

            _allFiresPaused = pause;

            if (_allFiresPaused) _allFires.ForEach(fire => fire.Pause());
            else _allFires.ForEach(fire => fire.Play());
        }

        private void CreateFire(Vector3 ignitionPoint)
        {
            var fire = new FireBehaviour(map, ignitionPoint);
            if (!fire.CanBurn()) return;
            
            _allFires.Add(fire);
            fire.Ignite();
        }


    }
}
