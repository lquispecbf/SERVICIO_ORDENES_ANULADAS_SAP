@echo off
setlocal
echo ===============================================
echo  DESINSTALANDO SERVICIO CBF ORDENES ANULADAS SAP
echo ===============================================
set SERVICE_NAME=CBFOrdenesAnuladasSAP

sc stop "%SERVICE_NAME%"
sc delete "%SERVICE_NAME%"

echo.
echo Servicio desinstalado correctamente.
pause

