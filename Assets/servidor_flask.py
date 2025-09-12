from flask import Flask, jsonify, request
from flask_cors import CORS
import json
import threading
import time
from reto import CUnity, CEstrategiaUnity, CAleatorioUnity

app = Flask(__name__)
CORS(app)

# Variables globales para la simulación
modelo_actual = None
simulacion_activa = False
auto_run = False
archivo_config = "txtxd.txt"

def crear_modelo():
    """Crea un nuevo modelo de simulación"""
    global modelo_actual
    modelo_actual = CEstrategiaUnity(archivo_config)
    return modelo_actual

def auto_step_loop():
    """Loop para ejecutar steps automáticamente cada segundo"""
    global auto_run, modelo_actual
    while True:
        if auto_run and modelo_actual and not modelo_actual.juegoTerminado:
            modelo_actual.step()
        time.sleep(1)

# Iniciar el hilo para auto-step
auto_thread = threading.Thread(target=auto_step_loop, daemon=True)
auto_thread.start()

@app.route('/inicializar', methods=['POST'])
def inicializar_simulacion():
    global simulacion_activa, modelo_actual
    try:
        data = request.get_json(force=True)
        modo = data.get("modo", "estrategia")

        if modo == "aleatorio":
            modelo_actual = CAleatorioUnity(archivo_config)
        else:
            modelo_actual = CEstrategiaUnity(archivo_config)

        simulacion_activa = True
        estado_inicial = modelo_actual.capturarEstadoActual()
        return jsonify({
            "status": "success",
            "message": f"Simulación {modo} inicializada correctamente",
            "estado": estado_inicial
        })
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/step', methods=['POST'])
def ejecutar_step():
    """Ejecuta un paso de la simulación"""
    global modelo_actual
    try:
        if not modelo_actual:
            return jsonify({"status": "error", "message": "Simulación no inicializada"}), 400
        
        if modelo_actual.juegoTerminado:
            estado_actual = modelo_actual.capturarEstadoActual()
            return jsonify({
                "status": "warning", 
                "message": "El juego ya terminó", 
                "estado": estado_actual
            })
        
        modelo_actual.step()
        estado_actual = modelo_actual.capturarEstadoActual()
        
        response = {
            "status": "success",
            "message": "Step ejecutado correctamente",
            "estado": estado_actual
        }
        return jsonify(response)
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/reiniciar', methods=['POST'])
def reiniciar_simulacion():
    """Reinicia la simulación"""
    global auto_run
    try:
        auto_run = False  # Pausar auto-run al reiniciar
        crear_modelo()
        estado_inicial = modelo_actual.capturarEstadoActual()
        
        response = {
            "status": "success",
            "message": "Simulación reiniciada correctamente",
            "estado": estado_inicial
        }
        return jsonify(response)
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/auto_run', methods=['POST', 'PUT'])
def toggle_auto_run():
    """Activa/desactiva la ejecución automática"""
    global auto_run
    try:
        # Manejar diferentes tipos de content-type
        if request.is_json:
            data = request.get_json()
        else:
            # Para WWWForm
            data = {"activate": not auto_run}
        
        if data and 'activate' in data:
            auto_run = bool(data['activate'])
        else:
            auto_run = not auto_run
        
        response = {
            "status": "success",
            "message": f"Auto-run {'activado' if auto_run else 'desactivado'}",
            "auto_run": auto_run
        }
        return jsonify(response)
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/estado', methods=['GET'])
def obtener_estado():
    """Obtiene el estado actual de la simulación"""
    try:
        if not modelo_actual:
            return jsonify({"status": "error", "message": "Simulación no inicializada"}), 400
        
        estado_actual = modelo_actual.capturarEstadoActual()
        
        response = {
            "status": "success",
            "estado": estado_actual,
            "auto_run": auto_run
        }
        return jsonify(response)
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/info', methods=['GET'])
def info_simulacion():
    """Información básica de la simulación"""
    global modelo_actual
    try:
        if not modelo_actual:
            return jsonify({
                "status": "no_initialized",
                "message": "Simulación no inicializada"
            })
        
        info = {
            "status": "initialized",
            "turno": modelo_actual.turno,
            "juego_terminado": modelo_actual.juegoTerminado,
            "resultado": modelo_actual.resultado,
            "auto_run": auto_run,
            "dimensiones": {
                "ancho": modelo_actual.width,
                "alto": modelo_actual.height
            },
            "estadisticas": {
                "victimas_rescatadas": modelo_actual.victimasRescatadas,
                "victimas_perdidas": modelo_actual.victimasPerdidas,
                "puntos_dano": modelo_actual.puntosDano,
                "fuegos_activos": len(modelo_actual.fuegoPosiciones),
                "humos_activos": len(modelo_actual.humoPosiciones)
            }
        }
        return jsonify(info)
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

if __name__ == '__main__':
    print("Servidor Flask iniciado en http://localhost:5000")
    print("Endpoints disponibles:")
    print("  POST /inicializar - Inicializa la simulación")
    print("  POST /step - Ejecuta un paso")
    print("  POST /reiniciar - Reinicia la simulación")
    print("  POST /auto_run - Activa/desactiva ejecución automática")
    print("  GET /estado - Obtiene el estado actual")
    print("  GET /info - Información básica")
    
    # Crear modelo inicial
    crear_modelo()
    
    app.run(host='localhost', port=5000, debug=True, threaded=True)