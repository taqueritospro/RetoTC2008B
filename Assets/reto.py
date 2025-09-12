# ======================================
# ========= Para instalar Mesa =========
# ======================================

#!pip install numpy scipy matplotlib seaborn scikit-learn mesa==3.0 -q # Descomentar si se usa google colab

# ======================================
# ======== Librerías Necesarias ========
# ======================================

# Requiero Mesa > 3.0.3
# Importamos las clases que se requieren para manejar los agentes (Agent) y su entorno (Model).
# Cada modelo puede contener múltiples agentes.
from mesa import Agent, Model

# Debido a que necesitamos que existe un solo agente por celda, elegimos ''SingleGrid''.
from mesa.space import MultiGrid

# Con ''RandomActivation'', hacemos que todos los agentes se activen de forma aleatoria.
from mesa.time import RandomActivation

# Haremos uso de ''DataCollector'' para obtener información de cada paso de la simulación.
from mesa.datacollection import DataCollector

# Haremos uso de ''batch_run'' para ejecutar varias simulaciones
from mesa.batchrunner import batch_run

# matplotlib lo usaremos crear una animación de cada uno de los pasos del modelo.
#%matplotlib inline
#import matplotlib
#import matplotlib.pyplot as plt
#import matplotlib.animation as animation
#plt.rcParams["animation.html"] = "jshtml"
#matplotlib.rcParams['animation.embed_limit'] = 2**128

# Importamos los siguientes paquetes para el mejor manejo de valores numéricos.
import numpy as np
import pandas as pd
import seaborn as sns
import random
sns.set()

# Definimos otros paquetes que vamos a usar para medir el tiempo de ejecución de nuestro algoritmo.
import time
import datetime

# Definimos los paquetes que vamos a usar para las estructuras de datos del modelo.
from enum import Enum

# Para el servidor en flask.
from flask import Flask, jsonify, request
from flask_cors import CORS
import json
import copy

# ======================================
# ========== Clases Bomberos ===========
# ======================================

# Clases de Enum para estados y tipos de los agentes y el escenario.
class CTipoBombero(Enum):
    APAGAFUEGOS = "apagafuegos"
    BUSCADOR = "buscador"
    SALVADOR = "salvador"
    ABRE_PUERTAS = "abre_puertas"

class CEstadoBombero(Enum):
    AFK = "afk"
    MOVIENDOSE_AL_OBJETIVO = "moviendose_al_objetivo"
    TRABAJANDO = "trabajando"
    LLEVANDO_VICTIMA = "llevando_victima"
    REGRESANDO_A_BASE = "regresando_a_base"

class CTipoCelda(Enum):
    PISO = "piso"
    PARED = "pared"
    PUERTA_CERRADA = "puerta_cerrada"
    PUERTA_ABIERTA = "puerta_abierta"
    ENTRADA = "entrada"
    SIN_DESCUBRIR = "sin_descubrir"

class CTipoMarcador(Enum):
    FUEGO = "fuego"
    HUMO = "humo"
    VICTIMA = "victima"
    FALSA_ALARMA = "falsa_alarma"
    POI = "poi"
    VICTIMA_ENCONTRADA = "victima_encontrada"
    DANO = "dano"

class CModoAgente(Enum):
    ALEATORIO = "aleatorio"
    ESTRATEGIA = "estrategia"

# Clase padre para todos los bomberos.
class CBombero(Agent):
    def __init__(self, idBombero, model, tipoBombero):
        super().__init__(model)
        self.unique_id = idBombero
        self.tipoBombero = tipoBombero
        self.estadoBombero = CEstadoBombero.AFK
        self.puntosAccion = 4
        self.maxPuntosAccion = 4
        self.posicionObjetivo = None
        self.llevandoVictima = False
        self.path = []
        self.modo = CModoAgente.ESTRATEGIA

    # Método que cambia el modo del agente.
    def cambiarModo(self, nuevoModo):
        self.modo = nuevoModo
        # Resetear estado cuando cambia el modo.
        self.estadoBombero = CEstadoBombero.AFK
        self.posicionObjetivo = None

# Método que reinicia los puntos de acción al inicio del turno.
    def recargarPuntosAccion(self):
        self.puntosAccion = self.maxPuntosAccion

# Método que verifica si puede moverse a una posición.
    def puedeMoverseA(self, posicion):
        if not self.model.esPosicionValida(posicion):
            return False
        if not self.model.esCeldaDescubierta(posicion):
            return False
        return True

# Método que calcula el costo de movimiento a una posición.
    def obtenerCostoMovimiento(self, posicion):
        costo = 1
        if self.model.tieneFuegoEn(posicion):
            costo = 2
        if self.model.tieneHumoEn(posicion):
            costo = 2
        return costo
    # Método que mueve al agente aleatoriamente.
    def movimientoAleatorio(self):
        posicionActual = self.pos

        # Obtener posiciones adyacentes válidas.
        posicionesAdyacentes = self.model.grid.get_neighborhood(
            posicionActual, moore=False, include_center=False
        )

        posicionesValidas = []
        for pos in posicionesAdyacentes:
            if self.puedeMoverseA(pos):
                posicionesValidas.append(pos)

        if posicionesValidas:
            # Seleccionar posición aleatoria.
            import random
            nuevaPosicion = random.choice(posicionesValidas)
            costo = self.obtenerCostoMovimiento(nuevaPosicion)

            if self.puntosAccion >= costo:
                self.model.grid.move_agent(self, nuevaPosicion)
                self.puntosAccion -= costo
                # Descubrir celdas adyacentes en la nueva posición.
                self.model.descubrirCeldasAdyacentesAlAgente(nuevaPosicion)
                return True

        return False

# Función que mueve el agente hacia un objetivo usando la distancia
# de Manhattan (pathfinding).
    def moverseAlObjetivo(self, posicionObjetivo):
        if not posicionObjetivo or posicionObjetivo == self.pos:
            return True

        posicionActual = self.pos

        # Descubrir celdas adyacentes al moverse
        self.model.descubrirCeldasAdyacentesAlAgente(posicionActual)

        dx = posicionObjetivo[0] - posicionActual[0]
        dy = posicionObjetivo[1] - posicionActual[1]

        nuevaX, nuevaY = posicionActual

        if abs(dx) > abs(dy):
            if dx != 0:
                nuevaX += 1 if dx > 0 else -1
            elif dy != 0:
                nuevaY += 1 if dy > 0 else -1
        else:
            if dy != 0:
                nuevaY += 1 if dy > 0 else -1
            elif dx != 0:
                nuevaX += 1 if dx > 0 else -1

        nuevaPosicion = (nuevaX, nuevaY)

        # Verificar si el movimiento se puede realizar.
        if self.puedeMoverseA(nuevaPosicion):
            costo = self.obtenerCostoMovimiento(nuevaPosicion)
            if self.puntosAccion >= costo:
                self.model.grid.move_agent(self, nuevaPosicion)
                self.puntosAccion -= costo
                # Descubrir celdas adyacentes en la nueva posición.
                self.model.descubrirCeldasAdyacentesAlAgente(nuevaPosicion)
                return nuevaPosicion == posicionObjetivo

        return False

# Método abstracto que sobreescriben las clases hijo
# se usa el concepto de polimorfismo; pass es como el
# continue del if.
    def step(self):
        pass

# Clase Apagafuegos - Se enfoca en extinguir el fuego.
class CApagafuegos(CBombero):
    def __init__(self, idBombero, model):
        super().__init__(idBombero, model, CTipoBombero.APAGAFUEGOS)

# Método para encontrar el fuego más cercano en las zonas descubiertas.
    def encontrarFuegoCercano(self):
        posicionesFuego = []
        for pos in self.model.obtenerPosicionesFuego():
            if self.model.esCeldaDescubierta(pos):
                posicionesFuego.append(pos)

        if not posicionesFuego:
            return None

        posicionActual = self.pos
        distanciaMinima = float('inf')
        fuegoCercano = None

        for posicionFuego in posicionesFuego:
            distancia = abs(posicionFuego[0] - posicionActual[0]) + abs(posicionFuego[1] - posicionActual[1])
            if distancia < distanciaMinima:
                distanciaMinima = distancia
                fuegoCercano = posicionFuego

        return fuegoCercano

# Método para apagar el fuego en la posición actual o adyacente.
    def apagarFuego(self):
        posicionActual = self.pos

        # Verificar si hay fuego en la posición actual.
        if self.model.tieneFuegoEn(posicionActual) and self.puntosAccion >= 2:
            self.model.apagarFuego(posicionActual)
            self.puntosAccion -= 2
            return True

        # Obtener posiciones adyacentes.
        posicionesAdyacentes = self.model.grid.get_neighborhood(posicionActual, moore=False, include_center=False)

        for posicionAdyacente in posicionesAdyacentes:
            if (self.model.esPosicionValida(posicionAdyacente) and
                self.model.tieneFuegoEn(posicionAdyacente) and
                self.puntosAccion >= 2):
                self.model.apagarFuego(posicionAdyacente)
                self.puntosAccion -= 2
                return True

        return False

    def step(self):
        if self.puntosAccion <= 0:
            return

        # Comportamiento según el modo.
        if self.modo == CModoAgente.ALEATORIO:
            # Si se puede apagar fuego en posición actual o adyacente, hacerlo.
            if self.apagarFuego():
                return

            # Moverse aleatoriamente.
            self.movimientoAleatorio()

        elif self.modo == CModoAgente.ESTRATEGIA:
            # Si se puede apagar fuego en posición actual o adyacente, hacerlo.
            if self.apagarFuego():
                return

            # Buscar el fuego más cercano y cambiar el estado del agente.
            if self.estadoBombero == CEstadoBombero.AFK:
                fuegoCercano = self.encontrarFuegoCercano()
                if fuegoCercano:
                    self.posicionObjetivo = fuegoCercano
                    self.estadoBombero = CEstadoBombero.MOVIENDOSE_AL_OBJETIVO

            # Mover el agente hacia el objetivo.
            if self.estadoBombero == CEstadoBombero.MOVIENDOSE_AL_OBJETIVO and self.posicionObjetivo:
                if self.moverseAlObjetivo(self.posicionObjetivo):
                    self.estadoBombero = CEstadoBombero.AFK
                    self.posicionObjetivo = None

# Clase Buscador - Busca víctimas y coloca marcadores.
class CBuscador(CBombero):
    def __init__(self, idBombero, model):
        super().__init__(idBombero, model, CTipoBombero.BUSCADOR)

# Método para encontrar el Poi más cercano.
    def encontrarPoiCercano(self):
        posicionesPoi = []
        for pos in self.model.obtenerPosicionesPoi():
            if self.model.esCeldaDescubierta(pos):
                posicionesPoi.append(pos)

        if not posicionesPoi:
            return None

        posicionActual = self.pos
        distanciaMinima = float('inf')
        poiCercano = None

        for posicionPoi in posicionesPoi:
            distancia = abs(posicionPoi[0] - posicionActual[0]) + abs(posicionPoi[1] - posicionActual[1])
            if distancia < distanciaMinima:
                distanciaMinima = distancia
                poiCercano = posicionPoi

        return poiCercano

# Método para buscar y revelar el POI en la posición actual.
    def buscarPoi(self):
        posicionActual = self.pos
        if self.model.tienePoiEn(posicionActual) and self.puntosAccion >= 1:
            res = self.model.revelarPoi(posicionActual)
            self.puntosAccion -= 1

            if res == "victima":
                if self.model.obtenerContadorVictimasEncontradas() < 2:
                    self.model.ponerMarcadorVictimaEncontrada(posicionActual)
                    self.model.notificarVictimaEncontrada(posicionActual)
                    return True
            elif res == "falsa_alarma":
                return True

        return False

    def step(self):
        if self.puntosAccion <= 0:
            return

        # Comportamiento según el modo.
        if self.modo == CModoAgente.ALEATORIO:
            # Si se encuentra en un POI, investigarlo.
            if self.buscarPoi():
                return

            # Moverse aleatoriamente.
            self.movimientoAleatorio()

        elif self.modo == CModoAgente.ESTRATEGIA:
            # Si se encuentra en un POI, investigarlo.
            if self.buscarPoi():
                return

            # Buscar el POI más cercano y cambiar estado del agente.
            if self.estadoBombero == CEstadoBombero.AFK:
                poiCercano = self.encontrarPoiCercano()
                if poiCercano:
                    self.posicionObjetivo = poiCercano
                    self.estadoBombero = CEstadoBombero.MOVIENDOSE_AL_OBJETIVO

            # Mover el agente hacia el objetivo.
            if self.estadoBombero == CEstadoBombero.MOVIENDOSE_AL_OBJETIVO and self.posicionObjetivo:
                if self.moverseAlObjetivo(self.posicionObjetivo):
                    self.estadoBombero = CEstadoBombero.AFK
                    self.posicionObjetivo = None

# Clase Salvador - Recoge víctimas encontradas y las lleva a la zona de rescate.
class CSalvador(CBombero):
    def __init__(self, idBombero, model):
        super().__init__(idBombero, model, CTipoBombero.SALVADOR)

# Método que busca a la víctima encontrada más cercana.
    def buscarVictimaEncontradaCercana(self):
        victimasEncontradas = self.model.obtenerPosicionesVictimaEncontrada()
        if not victimasEncontradas:
            return None

        posicionActual = self.pos
        distanciaMinima = float('inf')
        victimaCercana = None

        for posicionVictima in victimasEncontradas:
            distancia = abs(posicionVictima[0] - posicionActual[0]) + abs(posicionVictima[1] - posicionActual[1])
            if distancia < distanciaMinima:
                distanciaMinima = distancia
                victimaCercana = posicionVictima

        return victimaCercana

# Método que recoge a la victima de la posición actual.
    def recogerVictima(self):
        posicionActual = self.pos
        if (self.model.victimaEncontradaEn(posicionActual) and
            not self.llevandoVictima and
            self.puntosAccion >= 1):

            self.model.removerMarcadorVictimaEncontrada(posicionActual)
            self.llevandoVictima = True
            self.estadoBombero = CEstadoBombero.LLEVANDO_VICTIMA
            self.posicionObjetivo = self.model.obtenerEntradaCercana(posicionActual)
            self.puntosAccion -= 1
            return True
        return False

# Método para entregar la victima en la zona de rescate.
    def entregarVictima(self):
        posicionActual = self.pos
        if self.llevandoVictima and self.model.esEntrada(posicionActual):
            self.model.rescatarVictima()
            self.llevandoVictima = False
            self.estadoBombero = CEstadoBombero.AFK
            self.posicionObjetivo = None
            return True
        return False

    def step(self):
        if self.puntosAccion <= 0:
            return

        # Comportamiento según el modo.
        if self.modo == CModoAgente.ALEATORIO:
            # Si el agente está cargando una víctima y está en la entrada, entregarla.
            if self.llevandoVictima and self.entregarVictima():
                return

            # Si el agente está en una posición de víctima encontrada, recogerla.
            if not self.llevandoVictima and self.recogerVictima():
                return

            # Moverse aleatoriamente.
            self.movimientoAleatorio()

        elif self.modo == CModoAgente.ESTRATEGIA:
            # Si el agente está cargando una víctima y está en la entrada, entregarla.
            if self.llevandoVictima and self.entregarVictima():
                return

            # Si el agente está en una posición de víctima encontrada, recogerla.
            if not self.llevandoVictima and self.recogerVictima():
                return

            # Si el agente está cargando una víctima, moverse a la salida más cercana.
            if self.llevandoVictima and self.estadoBombero == CEstadoBombero.LLEVANDO_VICTIMA:
                if self.posicionObjetivo and self.moverseAlObjetivo(self.posicionObjetivo):
                    self.entregarVictima()

            # Buscar víctimas encontradas y cambiar estado del agente.
            elif self.estadoBombero == CEstadoBombero.AFK and not self.llevandoVictima:
                victimaCercana = self.buscarVictimaEncontradaCercana()
                if victimaCercana:
                    self.posicionObjetivo = victimaCercana
                    self.estadoBombero = CEstadoBombero.MOVIENDOSE_AL_OBJETIVO

            # Mover el agente hacia objetivo.
            elif self.estadoBombero == CEstadoBombero.MOVIENDOSE_AL_OBJETIVO and self.posicionObjetivo:
                if self.moverseAlObjetivo(self.posicionObjetivo):
                    self.estadoBombero = CEstadoBombero.AFK
                    self.posicionObjetivo = None

# Clase Abre Puertas - Descubre nuevas zonas abriendo puertas.
class CAbrePuertas(CBombero):
    def __init__(self, idBombero, model):
        super().__init__(idBombero, model, CTipoBombero.ABRE_PUERTAS)

# Método para encontrar la puerta cerrada más cercana.
    def encontrarPuertaCerradaCercana(self):
        puertasCerradas = []
        for pos in self.model.obtenerPosicionesPuertaCerrada():
            # Solo buscar puertas en zonas descubiertas o adyacentes a zonas descubiertas.
            if (self.model.esCeldaDescubierta(pos) or
                self.model.tieneCeldaAdyacenteDescubierta(pos)):
                puertasCerradas.append(pos)

        if not puertasCerradas:
            return None

        posicionActual = self.pos
        distanciaMinima = float('inf')
        puertaCercana = None

        for posicionPuerta in puertasCerradas:
            distancia = abs(posicionPuerta[0] - posicionActual[0]) + abs(posicionPuerta[1] - posicionActual[1])
            if distancia < distanciaMinima:
                distanciaMinima = distancia
                puertaCercana = posicionPuerta

        return puertaCercana

# Método para abrir la puerta de la posición actual.
    def abrirPuerta(self):
        posicionActual = self.pos
        if (self.model.tienePuertaCerradaEn(posicionActual) and
            self.puntosAccion >= 1):

            self.model.abrirPuerta(posicionActual)
            self.puntosAccion -= 1
            # Descubrir celdas adyacentes al abrir puerta.
            self.model.descubrirCeldasAdyacentes(posicionActual)
            return True
        return False

    def step(self):
        if self.puntosAccion <= 0:
            return

        # Comportamiento según el modo.
        if self.modo == CModoAgente.ALEATORIO:
            # Si el agente está en una puerta cerrada, abrirla.
            if self.abrirPuerta():
                return

            # Moverse aleatoriamente.
            self.movimientoAleatorio()

        elif self.modo == CModoAgente.ESTRATEGIA:
            # Si el agente está en una puerta cerrada, abrirla.
            if self.abrirPuerta():
                return

            # Buscar puerta cerrada más cercana y cambiar estado.
            if self.estadoBombero == CEstadoBombero.AFK:
                puertaCercana = self.encontrarPuertaCerradaCercana()
                if puertaCercana:
                    self.posicionObjetivo = puertaCercana
                    self.estadoBombero = CEstadoBombero.MOVIENDOSE_AL_OBJETIVO

            # Mover el agente hacia objetivo.
            if self.estadoBombero == CEstadoBombero.MOVIENDOSE_AL_OBJETIVO and self.posicionObjetivo:
                if self.moverseAlObjetivo(self.posicionObjetivo):
                    self.estadoBombero = CEstadoBombero.AFK
                    self.posicionObjetivo = None

# Función para crear el dream team de bomberos.
def crearBomberos(model, modo=CModoAgente.ESTRATEGIA):
    bomberos = []
    idBombero = 0

    # 2 Apagafuegos.
    for i in range(2):
        bombero = CApagafuegos(idBombero, model)
        bombero.modo = modo
        bomberos.append(bombero)
        idBombero += 1

    # 2 Buscadores.
    for i in range(2):
        bombero = CBuscador(idBombero, model)
        bombero.modo = modo
        bomberos.append(bombero)
        idBombero += 1

    # 1 Salvador.
    bombero = CSalvador(idBombero, model)
    bombero.modo = modo
    bomberos.append(bombero)
    idBombero += 1

    # 1 Abre Puertas.
    bombero = CAbrePuertas(idBombero, model)
    bombero.modo = modo
    bomberos.append(bombero)
    idBombero += 1

    return bomberos

# Función para cambiar el modo de todos los bomberos
def cambiarModoBomberos(bomberos, nuevoModo):
    for bombero in bomberos:
        bombero.cambiarModo(nuevoModo)

# ======================================
# ========== Modelo del Juego ==========
# ======================================

class CJuego(Model):

    def __init__(self, archivoConfiguracion=None):
        super().__init__()
	
        # Dimensiones de la grid.
        self.width = 8
        self.height = 6

        # Configuración de la grid.
        self.grid = MultiGrid(self.width, self.height, torus=False)
        self.schedule = RandomActivation(self)

        # Inicializar variables del juego.
        self.turno = 0
        self.victimasRescatadas = 0
        self.victimasPerdidas = 0
        self.puntosDano = 0
        self.victimasEncontradas = 0
        self.juegoTerminado = False
        self.resultado = None

        # Configuración del family game setup.
        self.totalVictimas = 10
        self.totalFalsasAlarmas = 5
        self.maxPuntosDano = 24
        self.maxVictimasPerdidas = 4
        self.victimasParaGanar = 7
        self.maxPoisActivos = 3

        # Estructuras de datos para el estado del juego.
        self.celdasDescubiertas = set()
        self.fuegoPosiciones = set()
        self.humoPosiciones = set()
        self.poiPosiciones = {}  # {(x, y): "victima"|"falsa_alarma"}
        self.victimasEncontradasPosiciones = set()
        self.puertasCerradas = set()
        self.puertasAbiertas = set()
        self.entradas = set()
        self.paredes = {}  # {(x, y): [arriba, izquierda, abajo, derecha]}
        self.gridPiso = {}  # {(x, y): True/False} - True = piso, False = pared
        self.puertaDirs = {}  # {(x,y): dir}

        self.cargarConfiguracion(archivoConfiguracion)

        # Crear y colocar bomberos.
        self.bomberos = crearBomberos(self)
        self.colocarBomberos()

        # Generar POIs iniciales.
        self.generarPoisIniciales()

        # Inicializar el datacollector.
        self.datacollector = DataCollector(
            model_reporters={
                "victimasRescatadas": "victimasRescatadas",
                "victimasPerdidas": "victimasPerdidas",
                "puntosDano": "puntosDano",
                "fuegosActivos": lambda m: len(m.fuegoPosiciones),
                "humosActivos": lambda m: len(m.humoPosiciones),
                "turno": "turno"
            }
        )

        # Descubrir celdas iniciales.
        for entrada in self.entradas:
            self.descubrirCelda(entrada)
            self.descubrirCeldasAdyacentes(entrada)

        # Propagar humo inicial alrededor del fuego.
        self.propagarHumoInicial()

    # Carga la configuración del txt.
    def cargarConfiguracion(self, archivo):
        if archivo is None:
            self.configuracionPorDefecto()
            return

        try:
            with open(archivo, 'r') as f:
                lineas = f.read().strip().split('\n')

            idx = 0

            # Cargar configuración de la grid.
            for y in range(self.height):
                fila = lineas[idx].split()
                for x in range(self.width):
                    celdaInfo = fila[x]
                    paredes = [int(d) for d in celdaInfo]
                    self.paredes[(x, y)] = paredes
                    self.gridPiso[(x, y)] = not all(p == 1 for p in paredes)
                idx += 1

            # Cargar POIs iniciales.
            poisIniciales = []
            for i in range(3):
                if idx < len(lineas):
                    datos = lineas[idx].split()
                    if len(datos) >= 3:
                        fila, col, tipo = int(datos[0])-1, int(datos[1])-1, datos[2]
                        if 0 <= fila < self.height and 0 <= col < self.width:
                            poisIniciales.append((col, fila, "victima" if tipo == "v" else "falsa_alarma"))
                    idx += 1

            for poi in poisIniciales[:3]:
                self.poiPosiciones[(poi[0], poi[1])] = poi[2]

            # Cargar posiciones de fuego.
            for i in range(10):
                if idx < len(lineas):
                    datos = lineas[idx].split()
                    if len(datos) >= 2:
                        fila, col = int(datos[0])-1, int(datos[1])-1
                        if 0 <= fila < self.height and 0 <= col < self.width:
                            self.fuegoPosiciones.add((col, fila))
                    idx += 1

            # Cargar puertas.
            for i in range(8):
                if idx < len(lineas):
                    datos = lineas[idx].split()
                    if len(datos) >= 4:
                        r1, c1, r2, c2 = int(datos[0])-1, int(datos[1])-1, int(datos[2])-1, int(datos[3])-1
                        if (0 <= r1 < self.height and 0 <= c1 < self.width and
                            0 <= r2 < self.height and 0 <= c2 < self.width):

                            # Detectar dirección según diferencia entre celdas
                            if r1 == r2:  # horizontal
                                if c1 < c2:
                                    self.puertasCerradas.add((c1, r1))
                                    self.puertaDirs[(c1, r1)] = 3  # derecha
                                    self.puertasCerradas.add((c2, r2))
                                    self.puertaDirs[(c2, r2)] = 1  # izquierda
                            elif c1 == c2:  # vertical
                                if r1 < r2:
                                    self.puertasCerradas.add((c1, r1))
                                    self.puertaDirs[(c1, r1)] = 2  # abajo
                                    self.puertasCerradas.add((c2, r2))
                                    self.puertaDirs[(c2, r2)] = 0  # arriba
                    idx += 1

            # Cargar entradas.
            for i in range(4):
                if idx < len(lineas):
                    datos = lineas[idx].split()
                    if len(datos) >= 2:
                        fila, col = int(datos[0])-1, int(datos[1])-1
                        if 0 <= fila < self.height and 0 <= col < self.width:
                            self.entradas.add((col, fila))
                    idx += 1

        except Exception as e:
            print(f"Error cargando configuración: {e}")

    # Método para generar los POIs iniciales.
    def generarPoisIniciales(self):
        while len(self.poiPosiciones) < self.maxPoisActivos:
            self.generarNuevoPoi()

    # Método para generar un nuevo POI.
    def generarNuevoPoi(self):
        posicionesLibres = []
        for x in range(self.width):
            for y in range(self.height):
                pos = (x, y)
                if (self.gridPiso[pos] and
                    pos not in self.poiPosiciones and
                    pos not in self.fuegoPosiciones and
                    pos not in self.humoPosiciones and
                    pos not in self.entradas and
                    pos not in self.puertasCerradas):
                    posicionesLibres.append(pos)

        if posicionesLibres:
            nuevaPos = self.random.choice(posicionesLibres)
            tipo = "victima" if self.random.random() < 0.6 else "falsa_alarma"
            self.poiPosiciones[nuevaPos] = tipo

    # Método para propagar el humo inicial.
    def propagarHumoInicial(self):
        for pos in list(self.fuegoPosiciones):
            adyacentes = self.grid.get_neighborhood(pos, moore=False, include_center=False)
            for adj in adyacentes:
                if (self.esPosicionValida(adj) and
                    adj not in self.fuegoPosiciones and
                    self.gridPiso[adj]):
                    self.humoPosiciones.add(adj)

    # Método para colocar bomberos en la grid.
    def colocarBomberos(self):
        for i, bombero in enumerate(self.bomberos):
            while True:
                x = self.random.randrange(self.width)
                y = self.random.randrange(self.height)
                pos = (x, y)
                if (self.gridPiso.get(pos, True) and
                    pos not in self.fuegoPosiciones and
                    pos not in self.humoPosiciones and
                    pos not in self.poiPosiciones and
                    pos not in self.puertasCerradas):
                    self.grid.place_agent(bombero, pos)
                    self.schedule.add(bombero)
                    break

    # Métodos de utilidad para el estado del juego.

    # Método que verifica si es una posición válida.
    def esPosicionValida(self, posicion):
        x, y = posicion
        return 0 <= x < self.width and 0 <= y < self.height

    # Método que verifica si es una celda descubierta.
    def esCeldaDescubierta(self, posicion):
        return posicion in self.celdasDescubiertas

    # Método para descubrir una celda en la posición.
    def descubrirCelda(self, posicion):
        if self.esPosicionValida(posicion):
            self.celdasDescubiertas.add(posicion)

    # Método para descubrir las celdas adyacentes a la posición.
    def descubrirCeldasAdyacentes(self, posicion):
        adyacentes = self.grid.get_neighborhood(posicion, moore=False, include_center=False)
        for adj in adyacentes:
            if self.esPosicionValida(adj):
                self.descubrirCelda(adj)

    # Método para descubrir celdas adyacentes al agente.
    def descubrirCeldasAdyacentesAlAgente(self, posicion):
        self.descubrirCelda(posicion)
        self.descubrirCeldasAdyacentes(posicion)

    # Método para verificar si hay fuego en la posición.
    def tieneFuegoEn(self, posicion):
        return posicion in self.fuegoPosiciones

    # Método para verificar si hay humo en la posición.
    def tieneHumoEn(self, posicion):
        return posicion in self.humoPosiciones

    # Método para verificar si hay un POI en la posición.
    def tienePoiEn(self, posicion):
        return posicion in self.poiPosiciones

    # Método para obtener las pociciones del fuego.
    def obtenerPosicionesFuego(self):
        return list(self.fuegoPosiciones)

    # Método para obtener las pociciones de los POIs.
    def obtenerPosicionesPoi(self):
        return list(self.poiPosiciones.keys())

    # Método para obtener las pociciones de víctimas encontradas.
    def obtenerPosicionesVictimaEncontrada(self):
        return list(self.victimasEncontradasPosiciones)

    # Método para obtener el contador de víctimas encontradas.
    def obtenerContadorVictimasEncontradas(self):
        return self.victimasEncontradas

    # Método para obtener las pociciones de víctimas encontradas.
    def victimaEncontradaEn(self, posicion):
        return posicion in self.victimasEncontradasPosiciones

    # Método para verificar si una posición es entrada.
    def esEntrada(self, posicion):
        return posicion in self.entradas

    # Método para obtener la entrada más cercana.
    def obtenerEntradaCercana(self, posicion):
        if not self.entradas:
            return None

        x, y = posicion
        entradaCercana = None
        distanciaMinima = float('inf')

        for entrada in self.entradas:
            ex, ey = entrada
            distancia = abs(ex - x) + abs(ey - y)
            if distancia < distanciaMinima:
                distanciaMinima = distancia
                entradaCercana = entrada

        return entradaCercana

    # Método para obtener las posiciones de puertas cerradas.
    def obtenerPosicionesPuertaCerrada(self):
        return list(self.puertasCerradas)

    # Método para verificar si la posición es una puerta cerrada.
    def tienePuertaCerradaEn(self, posicion):
        return posicion in self.puertasCerradas

    # Método para verificar si hay celdas adyacentes descubiertas.
    def tieneCeldaAdyacenteDescubierta(self, posicion):
        adyacentes = self.grid.get_neighborhood(posicion, moore=False, include_center=False)
        for adj in adyacentes:
            if adj in self.celdasDescubiertas:
                return True
        return False

    # Método para verificar el daño estructural.
    def verificarDanoEstructural(self):
        fuegos = list(self.fuegoPosiciones)

        # Horizontalmente.
        for y in range(self.height):
            for x in range(self.width - 2):
                consecutivos = 0
                for i in range(3):
                    if (x + i, y) in fuegos:
                        consecutivos += 1
                    else:
                        break
                # Si hay 3 fuegos consecutivos horizontalmente se añade daño estructural.
                if consecutivos == 3:
                    self.puntosDano += 1
                    return

        # Verticalmente.
        for x in range(self.width):
            for y in range(self.height - 2):
                consecutivos = 0
                for i in range(3):
                    if (x, y + i) in fuegos:
                        consecutivos += 1
                    else:
                        break
                # Si hay 3 fuegos consecutivos verticalmente se añade daño estructural.
                if consecutivos == 3:
                    self.puntosDano += 1
                    return

    # Método para verificar si un POI fue alcanzado por fuego.
    def verificarPoisAlcanzadosPorFuego(self):
        poisAEliminar = []
        for pos, tipo in self.poiPosiciones.items():
            if pos in self.fuegoPosiciones:
                poisAEliminar.append((pos, tipo))

        for pos, tipo in poisAEliminar:
            del self.poiPosiciones[pos]
            if tipo == "victima":
                self.victimasPerdidas += 1
            self.generarNuevoPoi()

    # Métodos de los agentes.

    # Método para apagar el fuego en la posición.
    def apagarFuego(self, posicion):
        if posicion in self.fuegoPosiciones:
            self.fuegoPosiciones.remove(posicion)
            self.humoPosiciones.add(posicion)
            return True
        return False

    # Método para apagar el revelar el POI en la posición.
    def revelarPoi(self, posicion):
        if posicion in self.poiPosiciones:
            tipo = self.poiPosiciones[posicion]
            del self.poiPosiciones[posicion]
            self.generarNuevoPoi()
            return tipo
        return None

    # Método para colocar un marcador de victima encontrada.
    def ponerMarcadorVictimaEncontrada(self, posicion):
        self.victimasEncontradasPosiciones.add(posicion)

    # Método para remover un marcador de victima encontrada.
    def removerMarcadorVictimaEncontrada(self, posicion):
        if posicion in self.victimasEncontradasPosiciones:
            self.victimasEncontradasPosiciones.remove(posicion)

    # Método para notificar que una victima fue encontrada.
    def notificarVictimaEncontrada(self, posicion):
        self.victimasEncontradas += 1

    # Método para rescatar una víctima.
    def rescatarVictima(self):
        self.victimasRescatadas += 1
        if self.victimasEncontradas > 0:
            self.victimasEncontradas -= 1

    # Método para abrir una puerta.
    def abrirPuerta(self, posicion):
        if posicion in self.puertasCerradas:
            self.puertasCerradas.remove(posicion)
            self.puertasAbiertas.add(posicion)
            return True
        return False

    # Método para verificar cuando el juego termina.
    def verificarCondicionesFin(self):
        if self.victimasRescatadas >= self.victimasParaGanar:
            self.juegoTerminado = True
            self.resultado = "🏆."
        elif self.victimasPerdidas >= self.maxVictimasPerdidas:
            self.juegoTerminado = True
            self.resultado = "💀."
        elif self.puntosDano >= self.maxPuntosDano:
            self.juegoTerminado = True
            self.resultado = "💥."

    # Método para procesar la expansión del fuego.
    def procesarHumoYFuego(self):
        posicionesValidas = []

        for x in range(self.width):
            for y in range(self.height):
                posicion = (x, y)
                if (self.esPosicionValida(posicion) and
                    self.gridPiso[posicion] and
                    self.esCeldaDescubierta(posicion)):
                    posicionesValidas.append(posicion)

        if not posicionesValidas:
            return

        posicionElegida = self.random.choice(posicionesValidas)

        if posicionElegida not in self.humoPosiciones and posicionElegida not in self.fuegoPosiciones:
            self.humoPosiciones.add(posicionElegida)

        elif posicionElegida in self.humoPosiciones:
            self.humoPosiciones.remove(posicionElegida)
            self.fuegoPosiciones.add(posicionElegida)
            self.verificarPoisAlcanzadosPorFuego()
            self.verificarDanoEstructural()

        elif posicionElegida in self.fuegoPosiciones:
            adyacentes = self.grid.get_neighborhood(posicionElegida, moore=False, include_center=False)

            celdasValidas = []
            for adj in adyacentes:
                if (self.esPosicionValida(adj) and
                    adj not in self.fuegoPosiciones and
                    self.gridPiso[adj] and
                    self.esCeldaDescubierta(adj)):
                    celdasValidas.append(adj)

            if celdasValidas:
                celdaExpansion = self.random.choice(celdasValidas)
                self.fuegoPosiciones.add(celdaExpansion)

                if celdaExpansion in self.humoPosiciones:
                    self.humoPosiciones.remove(celdaExpansion)

                self.verificarPoisAlcanzadosPorFuego()
                self.verificarDanoEstructural()

    def step(self):
        if self.juegoTerminado:
            return

        self.turno += 1

        # Recargar puntos de acción de todos los bomberos.
        for bombero in self.bomberos:
            bombero.recargarPuntosAccion()

        # Ejecutar acciones de los agentes.
        self.schedule.step()

        # Procesar la lógica de humo y fuego.
        self.procesarHumoYFuego()

        # Verificar condiciones de fin.
        self.verificarCondicionesFin()

        # Recolectar datos.
        self.datacollector.collect(self)

    def runModel(self):
        stepCount = 0
        while not self.juegoTerminado:
            self.step()
            #stepCount += 1

        return {
            "resultado": self.resultado,
            "victimasRescatadas": self.victimasRescatadas,
            "victimasPerdidas": self.victimasPerdidas,
            "puntosDano": self.puntosDano,
            "turnos": self.turno,
            "fuegosFinales": len(self.fuegoPosiciones),
            "humosFinales": len(self.humoPosiciones),
            "poisRestantes": len(self.poiPosiciones)
        }

# ======================================
# ========== Modelo Aleatorio ==========
# ======================================

class CAleatorio(CJuego):

    def __init__(self, archivoConfiguracion=None):
        super().__init__(archivoConfiguracion)
        # Cambiar todos los bomberos al modo aleatorio.
        cambiarModoBomberos(self.bomberos, CModoAgente.ALEATORIO)

    def runModel(self):

        while not self.juegoTerminado:
            self.step()

        if not self.juegoTerminado:
            self.juegoTerminado = True
            self.resultado = "Modo Aleatorio"

        return {
            "resultado": self.resultado,
            "victimasRescatadas": self.victimasRescatadas,
            "victimasPerdidas": self.victimasPerdidas,
            "puntosDano": self.puntosDano,
            "turnos": self.turno,
            "fuegosFinales": len(self.fuegoPosiciones),
            "humosFinales": len(self.humoPosiciones),
            "poisRestantes": len(self.poiPosiciones)
        }

# ======================================
# ========= Modelo Estrategia ==========
# ======================================

class CEstrategia(CJuego):

    def __init__(self, archivoConfiguracion=None):
        super().__init__(archivoConfiguracion)
        # Cambiar todos los bomberos al modo estrategia.
        cambiarModoBomberos(self.bomberos, CModoAgente.ESTRATEGIA)

    def runModel(self):

        while not self.juegoTerminado:
            self.step()

        if not self.juegoTerminado:
            self.juegoTerminado = True
            self.resultado = "Modo Estrategia"

        return {
            "resultado": self.resultado,
            "victimasRescatadas": self.victimasRescatadas,
            "victimasPerdidas": self.victimasPerdidas,
            "puntosDano": self.puntosDano,
            "turnos": self.turno,
            "fuegosFinales": len(self.fuegoPosiciones),
            "humosFinales": len(self.humoPosiciones),
            "poisRestantes": len(self.poiPosiciones)
        }

# ======================================
# ========= Clase para Unity ===========
# ======================================

class CUnity(CJuego):
    def __init__(self, archivoConfiguracion=None):
        super().__init__(archivoConfiguracion)
        self.estadosSimulacion = []
        self.simulacionActiva = False
        self.pasoActual = 0
        
        # Capturar estado inicial
        self.capturarEstadoActual()
    
    # Convertir puertas a lista de marcadores con dirección
    def convertir_puerta(self, puerta):
        x, y, dir = puerta
        return {"x": x, "y": y, "tipo": str(dir)}
        # Método que captura el estado actual para Unity.

    def capturarEstadoActual(self):
        
        # Información de los bomberos.
        bomberosInfo = []
        for bombero in self.bomberos:
            bomberoData = {
                "id": bombero.unique_id,
                "tipo": bombero.tipoBombero.value,
                "posicion": {
                    "x": bombero.pos[0] if bombero.pos else 0,
                    "y": bombero.pos[1] if bombero.pos else 0
                },
                "estado": bombero.estadoBombero.value,
                "puntosAccion": bombero.puntosAccion,
                "maxPuntosAccion": bombero.maxPuntosAccion,
                "llevandoVictima": bombero.llevandoVictima,
                "posicionObjetivo": {
                    "x": bombero.posicionObjetivo[0] if bombero.posicionObjetivo else -1,
                    "y": bombero.posicionObjetivo[1] if bombero.posicionObjetivo else -1
                },
                "modo": bombero.modo.value
            }
            bomberosInfo.append(bomberoData)
        
        # Información del grid.
        gridInfo = []
        for y in range(self.height):       # recorre filas
            for x in range(self.width):    # recorre columnas
                pos = (x, y)
                celdaData = {
                    "x": x,
                    "y": y,
                    "esPiso": self.gridPiso.get(pos, False),
                    "esDescubierta": pos in self.celdasDescubiertas,
                    "tieneFuego": pos in self.fuegoPosiciones,
                    "tieneHumo": pos in self.humoPosiciones,
                    "tienePoi": pos in self.poiPosiciones,
                    "tipoPoi": self.poiPosiciones.get(pos, ""),
                    "tieneVictimaEncontrada": pos in self.victimasEncontradasPosiciones,
                    "esPuertaCerrada": pos in self.puertasCerradas,
                    "esPuertaAbierta": pos in self.puertasAbiertas,
                    "esEntrada": pos in self.entradas,
                    "paredes": self.paredes.get(pos, [0, 0, 0, 0])
                      }
                gridInfo.append(celdaData)
        
        # Información de marcadores/efectos
        marcadores = {
            "fuego": [{"x": pos[0], "y": pos[1]} for pos in self.fuegoPosiciones],
            "humo": [{"x": pos[0], "y": pos[1]} for pos in self.humoPosiciones],
            "pois": [{"x": pos[0], "y": pos[1], "tipo": tipo} for pos, tipo in self.poiPosiciones.items()],
            "victimasEncontradas": [{"x": pos[0], "y": pos[1]} for pos in self.victimasEncontradasPosiciones],
            "puertasCerradas": [{"x": x, "y": y, "tipo": str(self.puertaDirs.get((x,y),0))} for (x, y) in self.puertasCerradas],
            "puertasAbiertas": [{"x": x, "y": y, "tipo": str(self.puertaDirs.get((x,y),0))} for (x, y) in self.puertasAbiertas],
            "entradas": [{"x": pos[0], "y": pos[1]} for pos in self.entradas]
        }
        
        # Estadísticas del juego
        estadisticas = {
            "turno": self.turno,
            "victimasRescatadas": self.victimasRescatadas,
            "victimasPerdidas": self.victimasPerdidas,
            "victimasEncontradas": self.victimasEncontradas,
            "puntosDano": self.puntosDano,
            "juegoTerminado": self.juegoTerminado,
            "resultado": self.resultado,
            "totalFuegosActivos": len(self.fuegoPosiciones),
            "totalHumosActivos": len(self.humoPosiciones),
            "totalPoisActivos": len(self.poiPosiciones),
            "maxPuntosDano": self.maxPuntosDano,
            "maxVictimasPerdidas": self.maxVictimasPerdidas,
            "victimasParaGanar": self.victimasParaGanar
        }
        
        # Estado completo
        estado = {
            "paso": self.pasoActual,
            "dimensiones": {
                "ancho": self.width,
                "alto": self.height
            },
            "bomberos": bomberosInfo,
            "grid": gridInfo,
            "marcadores": marcadores,
            "estadisticas": estadisticas,
            "timestamp": self.turno
        }
        
        self.estadosSimulacion.append(copy.deepcopy(estado))
        return estado
    
    def step(self):
        if self.juegoTerminado:
            return
        
        super().step()
        self.pasoActual += 1
        self.capturarEstadoActual()
    
    def obtenerEstados(self):
        return self.estadosSimulacion
    
    def obtenerEstadoPaso(self, paso):
        if 0 <= paso < len(self.estadosSimulacion):
            return self.estadosSimulacion[paso]
        return None
    
    def reiniciarSimulacion(self):
        self.__init__(self.archivoConfiguracion if hasattr(self, 'archivoConfiguracion') else None)

# ======================================
# ======= Aleatorio para Unity =========
# ======================================

class CAleatorioUnity(CUnity, CAleatorio):
    def __init__(self, archivoConfiguracion=None):
        CUnity.__init__(self, archivoConfiguracion)
        # Cambiar todos los bomberos al modo aleatorio.
        cambiarModoBomberos(self.bomberos, CModoAgente.ALEATORIO)

# ======================================
# ====== Estrategia para Unity =========
# ======================================

class CEstrategiaUnity(CUnity, CEstrategia):
    def __init__(self, archivoConfiguracion=None):
        CUnity.__init__(self, archivoConfiguracion)
        # Cambiar todos los bomberos al modo estrategia.
        cambiarModoBomberos(self.bomberos, CModoAgente.ESTRATEGIA)

# ======================================
# ====== Resultados Comparacion ========
# ======================================

def ejecutarMultiplesIteraciones(num_iteraciones, archivo_config, tipo_solucion):
    resultados = []

    for i in range(num_iteraciones):
        if tipo_solucion == "aleatoria":
            modelo = CAleatorio(archivo_config)
        elif tipo_solucion == "estrategica":
            modelo = CEstrategia(archivo_config)
        else:
            raise ValueError(f"Tipo de solución no válido: {tipo_solucion}")

        resultado = modelo.runModel()
        resultados.append(resultado)
    return resultados

def analizarResultados(resultados, nombre_estrategia):
    print(f"\n=== ESTADÍSTICAS - {nombre_estrategia.upper()} ===")

    # Métricas a analizar
    metricas = ['victimasRescatadas', 'victimasPerdidas', 'puntosDano', 'turnos', 'fuegosFinales', 'humosFinales']

    for metrica in metricas:
        valores = [r[metrica] for r in resultados]

        minimo = min(valores)
        maximo = max(valores)
        promedio = sum(valores) / len(valores)

        print(f"{metrica}:")
        print(f"  Mín: {minimo}")
        print(f"  Pro: {promedio:.2f}")
        print(f"  Máx: {maximo}")

    # Contar resultados
    resultados_conteo = {}
    for r in resultados:
        resultado = r['resultado']
        resultados_conteo[resultado] = resultados_conteo.get(resultado, 0) + 1

    print(f"\nResultados finales:")
    for resultado, count in resultados_conteo.items():
        porcentaje = (count / len(resultados)) * 100
        print(f"  {resultado}: {count} ({porcentaje:.1f}%)")

# Ejecutar análisis
NUM_ITERACIONES = 1000

print("Ejecutando análisis de múltiples iteraciones...")
print(f"Número de iteraciones por estrategia: {NUM_ITERACIONES}")

# Analizar estrategia aleatoria
print("\n--- Ejecutando estrategia ALEATORIA ---")
resultados_aleatorios = ejecutarMultiplesIteraciones(NUM_ITERACIONES, "txtxd.txt", "aleatoria")
analizarResultados(resultados_aleatorios, "ALEATORIA")

# Analizar estrategia estratégica
print("\n--- Ejecutando estrategia ESTRATÉGICA ---")
resultados_estrategicos = ejecutarMultiplesIteraciones(NUM_ITERACIONES, "txtxd.txt", "estrategica")
analizarResultados(resultados_estrategicos, "ESTRATÉGICA")

print(f"\n=== COMPARACIÓN DE PROMEDIOS ===")
print(f"Promedio víctimas rescatadas:")
print(f"  Solución Aleatoria: {sum(r['victimasRescatadas'] for r in resultados_aleatorios) / len(resultados_aleatorios):.2f}")
print(f"  Solución Estratégica: {sum(r['victimasRescatadas'] for r in resultados_estrategicos) / len(resultados_estrategicos):.2f}")

print(f"Promedio turnos:")
print(f"  Solución Aleatoria: {sum(r['turnos'] for r in resultados_aleatorios) / len(resultados_aleatorios):.2f}")
print(f"  Solución Estratégica: {sum(r['turnos'] for r in resultados_estrategicos) / len(resultados_estrategicos):.2f}")
