# DeliveryRoutes

Aplicación de consola en C# (.NET 10) que calcula la ruta de un domiciliario en moto. Recibe el mapa de la ciudad (ubicaciones unidas por calles con su distancia en metros, algunas de un solo sentido), la lista de paquetes con su punto de recogida y su punto de entrega, y la posición donde arranca el domiciliario. Con eso arma un recorrido que recoge y entrega todos los paquetes, entregando cada uno después de recogerlo, y trata de que la distancia total sea la menor posible. La ruta termina en la última entrega, no hay que volver al inicio.

## Cómo ejecutarlo

Desde la carpeta del proyecto:

```
dotnet run -- city.txt packages.txt D
```

Los argumentos son el archivo del mapa, el archivo de paquetes y la posición inicial. Si se ejecuta sin argumentos (`dotnet run`), el programa pide los tres datos por consola.

## Estrategia

El problema tiene dos partes y las resolví de forma distinta.

La primera es saber cuánto mide el mejor camino entre dos puntos del mapa. Eso sí se puede calcular exacto con Dijkstra. Las calles tienen distancias distintas, así que no sirve contar cuadras como haría BFS: un camino de tres calles cortas puede ser más corto que uno de dos calles largas. Además hay calles de un solo sentido, entonces la distancia de X a Y no siempre es la misma que de Y a X, y el programa nunca supone que lo sea. Solo importan 41 puntos (la posición inicial y los sitios donde hay que recoger o entregar algo), así que corro Dijkstra una vez desde cada uno y guardo las distancias y los caminos en una tabla. De ahí en adelante el planificador solo consulta esa tabla.

La segunda parte es decidir en qué orden hacer las 60 paradas, y esa no se puede resolver exacta. Con la condición de recoger antes de entregar hay 60! / 2^30 órdenes posibles, un número de 73 cifras. Aunque el computador revisara miles de millones de rutas por segundo no terminaría nunca, así que para el orden usé una heurística: no garantiza el óptimo, pero llega a una ruta muy buena en pocos segundos.

Lo primero que probé fue el vecino más cercano: estando en un punto, ir a la parada válida más cercana (recoger un paquete que falta o entregar uno que ya llevo). Es fácil, pero se deja tareas sueltas que después obligan a devolverse lejos, y da 40.52 km.

La heurística final tiene tres piezas:

- **Inserción más barata.** Se arranca con la ruta vacía y se van metiendo los paquetes uno por uno. Para cada paquete pruebo todas las posiciones posibles para su recogida y su entrega, siempre con la recogida antes, y me quedo con la que menos alarga la ruta. El aumento se calcula solo con los tramos que cambian: si meto una parada entre A y B, quito el tramo A a B y sumo A a la parada y la parada a B.
- **Reubicar un paquete.** Saco la recogida y la entrega de un paquete y las vuelvo a insertar con la inserción más barata. Si la ruta queda más corta, me quedo con el cambio. Esto lo repito para todos los paquetes mientras haya mejoras.
- **Reubicar una parada suelta.** Saco una sola parada y la pruebo en las demás posiciones donde la ruta siga siendo válida: una recogida solo puede ir antes de su entrega y una entrega solo después de su recogida. Si alguna posición acorta la ruta, la muevo ahí.

Las dos reubicaciones se alternan hasta que ninguna mejora nada. Como el resultado depende del orden en que se insertan los paquetes, todo el proceso se repite 400 veces: la primera con el orden del archivo y las demás con órdenes barajados al azar. Me quedo con la ruta más corta de todos los intentos. El azar usa una semilla fija, así que dos ejecuciones dan exactamente la misma ruta.

| Estrategia | Recorrido total |
|---|---|
| Vecino más cercano | 40.52 km |
| Inserción más barata con reubicaciones y 400 reinicios | 30.38 km |

La heurística final recorre 10.14 km menos, más o menos un 25 % menos que el vecino más cercano.

## Salida con city.txt, packages.txt y posición inicial D

```
Posición inicial: D
Paquetes: 30   Ubicaciones: 100   Calles: 142 (30 de un solo sentido)

 1. En D: recoger paquete 18.
 2. Ir de D a AR (1110 m): recoger paquete 20.
      Camino: D -> BT -> P -> CB -> AD -> AR
 3. Ir de AR a AJ (550 m): recoger paquetes 23 y 26.
      Camino: AR -> BB -> AJ
 4. Ir de AJ a BB (400 m): recoger paquete 16.
      Camino: AJ -> BB
 5. Ir de BB a BE (1260 m): recoger paquete 7.
      Camino: BB -> AR -> AD -> CB -> P -> BT -> BE
 6. Ir de BE a X (490 m): recoger paquete 12.
      Camino: BE -> E -> X
 7. Ir de X a BU (560 m): recoger paquetes 8 y 28.
      Camino: X -> B -> BU
 8. Ir de BU a CN (430 m): recoger paquetes 1, 2 y 3.
      Camino: BU -> CN
 9. Ir de CN a AM (780 m): recoger paquete 30.
      Camino: CN -> BU -> AM
10. Ir de AM a AX (270 m): recoger paquete 17.
      Camino: AM -> AK -> AX
11. Ir de AX a AQ (170 m): recoger paquete 9.
      Camino: AX -> AQ
12. Ir de AQ a BS (250 m): recoger paquete 25.
      Camino: AQ -> BS
13. Ir de BS a CS (310 m): recoger paquete 19.
      Camino: BS -> CS
14. Ir de CS a CR (830 m): entregar paquete 20.
      Camino: CS -> BS -> CT -> CR
15. Ir de CR a CH (680 m): recoger paquete 10.
      Camino: CR -> BO -> CJ -> CH
16. Ir de CH a CU (1050 m): recoger paquete 15.
      Camino: CH -> CJ -> CV -> BX -> CU
17. Ir de CU a M (1060 m): entregar paquete 1.
      Camino: CU -> CV -> W -> CM -> M
18. Ir de M a AE (430 m): entregar paquete 26.
      Camino: M -> CM -> AE
19. Ir de AE a AP (370 m): recoger paquete 5.
      Camino: AE -> AP
20. Ir de AP a AB (1760 m): recoger paquete 4.
      Camino: AP -> AM -> AK -> AX -> F -> BV -> AB
21. Ir de AB a BV (470 m): entregar paquete 19.
      Camino: AB -> BV
22. Ir de BV a BW (1890 m): entregar paquete 30.
      Camino: BV -> F -> AX -> S -> K -> N -> BW
23. Ir de BW a CL (270 m): recoger paquete 14.
      Camino: BW -> CL
24. Ir de CL a R (360 m): recoger paquetes 6 y 21.
      Camino: CL -> R
25. Ir de R a BN (810 m): entregar paquete 7.
      Camino: R -> CL -> BN
26. Ir de BN a AO (1300 m): entregar paquete 3 y recoger paquete 11.
      Camino: BN -> Y -> AV -> CG -> AI -> AO
27. Ir de AO a AI (170 m): recoger paquete 24.
      Camino: AO -> AI
28. Ir de AI a BM (550 m): entregar paquetes 2 y 10.
      Camino: AI -> AS -> BM
29. Ir de BM a BI (1750 m): recoger paquete 13.
      Camino: BM -> AS -> BH -> BL -> CO -> BI
30. Ir de BI a BY (990 m): entregar paquete 16.
      Camino: BI -> CO -> AZ -> V -> O -> BY
31. Ir de BY a Q (390 m): recoger paquetes 22 y 29.
      Camino: BY -> Q
32. Ir de Q a AA (1450 m): entregar paquete 12.
      Camino: Q -> BJ -> CI -> Z -> AA
33. Ir de AA a AC (900 m): entregar paquete 17.
      Camino: AA -> Z -> CI -> AC
34. Ir de AC a CD (130 m): entregar paquete 25.
      Camino: AC -> CD
35. Ir de CD a BF (710 m): entregar paquetes 18 y 23.
      Camino: CD -> BA -> AG -> BF
36. Ir de BF a AP (760 m): entregar paquetes 11 y 22.
      Camino: BF -> X -> B -> AP
37. Ir de AP a AT (350 m): entregar paquetes 14 y 15 y recoger paquete 27.
      Camino: AP -> AT
38. Ir de AT a AQ (200 m): entregar paquete 6.
      Camino: AT -> AQ
39. Ir de AQ a CT (350 m): entregar paquete 4.
      Camino: AQ -> BS -> CT
40. Ir de CT a CV (600 m): entregar paquete 28.
      Camino: CT -> BK -> CV
41. Ir de CV a T (820 m): entregar paquetes 24 y 27.
      Camino: CV -> W -> CF -> T
42. Ir de T a BE (440 m): entregar paquete 21.
      Camino: T -> BE
43. Ir de BE a CP (460 m): entregar paquete 9.
      Camino: BE -> BT -> CP
44. Ir de CP a AD (670 m): entregar paquetes 5 y 13.
      Camino: CP -> BT -> P -> CB -> AD
45. Ir de AD a AR (280 m): entregar paquete 8.
      Camino: AD -> AR
46. Ir de AR a AJ (550 m): entregar paquete 29.
      Camino: AR -> BB -> AJ

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 30.38 km
```

## Salida del ejemplo pequeño con city_mini.txt, packages_mini.txt y posición inicial A

```
Posición inicial: A
Paquetes: 3   Ubicaciones: 7   Calles: 7 (1 de un solo sentido)

 1. Ir de A a C (400 m): recoger paquete 1.
      Camino: A -> B -> C
 2. Ir de C a E (350 m): recoger paquete 2.
      Camino: C -> B -> E
 3. Ir de E a G (270 m): entregar paquete 1 y recoger paquete 3.
      Camino: E -> F -> G
 4. Ir de G a A (820 m): entregar paquete 3.
      Camino: G -> F -> E -> B -> A
 5. Ir de A a D (600 m): entregar paquete 2.
      Camino: A -> B -> C -> D

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 2.44 km
```
