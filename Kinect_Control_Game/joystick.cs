
namespace Kinect_Control_Juego
{
    using System;

    // Librería del Joystick Virtual
    using vJoyInterfaceWrap;

    public class Joystick
    {
        // Variables Globales


        // Declaring one joystick (Device id 1) and a position structure. 
        [CLSCompliant(false)]
        public vJoy mijoystick;
        [CLSCompliant(false)]
        public vJoy.JoystickState iReport; //Estructura que guarda la posición de los botones, los ejes y pov
        [CLSCompliant(false)]
        public uint id = 1; //número de identificación del dispositivo vJoy
        public long maxval = 0;

        //Funcion para inicializamos el joystick que creamos
        public bool Inicializa()
        {
            
            mijoystick = new vJoy();
            iReport = new vJoy.JoystickState();

            // Get the state of the requested device
            VjdStat status = mijoystick.GetVJDStatus(id);
            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!mijoystick.AcquireVJD(id))))
            {
                // Dar mensaje de error
                return false;
            }

            // Reset this device to default values
            mijoystick.ResetVJD(id);
            mijoystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxval);
            return true;
        }

        /* Funciones para configurar los joystick 
         * No se usan en este caso pero se dejan por demostracion
         * 
        public bool Girox(double intensidad) 
        {
            int x = (int)(intensidad);
            if (x > maxval) x = (int)maxval;
            return mijoystick.SetAxis(x, id, HID_USAGES.HID_USAGE_X);
        }

        public bool Giroy(double intensidad)
        {
            int y = (int)(intensidad);
            if (y > maxval) y = (int)maxval;
            return mijoystick.SetAxis(y, id, HID_USAGES.HID_USAGE_Y);
        }

        public bool Giroz(double intensidad)
        {
            int z = (int)(intensidad);
            if (z > maxval) z = (int)maxval;
            return mijoystick.SetAxis(z, id, HID_USAGES.HID_USAGE_Z);
        }
        public bool Girorz(double intensidad)
        {
            int rz = (int)(intensidad);
            if (rz > maxval) rz = (int)maxval;
            return mijoystick.SetAxis(rz, id, HID_USAGES.HID_USAGE_RZ);
        }
       */

        [CLSCompliant(false)]

        //Funcion para configurar apretar un boton
        public bool PulsBoton(uint num)
        {
            bool res;
            res = mijoystick.SetBtn(true, id, num); // Ponemos el boton a true para simular que lo apretamos
            System.Threading.Thread.Sleep(1000);    // Tiempo que simulamos que lo dejamos a pretado
            res = mijoystick.SetBtn(false, id, num); // Lo ponemos a false para dejar de apretarlo
            return res;
        }

        //Funcion para configurrar la cruceta
        public bool con_POV(int dir) // Dir es la direcccion de la vista: Arriba(1) Derecha(2), Abajo(3), Izquierda(0), Centro(-1)S
        {
            bool res;
            res = mijoystick.SetContPov(dir, id, 1); //se pone en la direccion solicitada
            System.Threading.Thread.Sleep(200);
            res = mijoystick.SetContPov(-1, id, 1); // tras una espera de 200ms se centra
            System.Threading.Thread.Sleep(200);
            return res;
        }

        //Funcion para mantener la pulsado la cruceta
        public bool ManPul_POV(int dir) // Dir es la direcccion de la vista: arriba(1) derecha(2), abajo(3), izquierda(0), centro(-1)
        {
            bool res;
            res = mijoystick.SetContPov(dir, id, 1); // Se pone en la direccion solicitada
            return res;
        }
 
        //Funcion para mantener pulsado el boton
        public bool ManPulsBoton(uint num)
        {
            bool res;
            res = mijoystick.SetBtn(true, id, num); // Lo ponemos a true para mantener el boton pulsado
            return res;
        }

        //Funcion para resetear los valores de la Cruceta
        public bool ResetManPul_POV(int dir) // Dir: Arriba(0), Izquierda(1), Abajo(2), Derecha(3), 
        {
            bool res;
            res = mijoystick.SetContPov(dir, id, 1); // Pulsamo la cruceta: dir indica la direccion a la que pulsamos
            System.Threading.Thread.Sleep(1000); // Tiempo que lo mantenemos pusado
            res = mijoystick.SetContPov(-1, id, 1); // Tras la espera volvemos a centrarlo
            return res;
        }
        
        //Funcion para resetear el boton
        public bool ResetPulsBoton(uint num)
        {
            bool res;
            res = mijoystick.SetBtn(false, id, num); //Lo ponemos a false para que deje de estar pulsado
            return res;
        }

    }
}
