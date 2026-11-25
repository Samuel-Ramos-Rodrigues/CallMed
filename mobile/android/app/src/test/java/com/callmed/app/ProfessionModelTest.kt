package com.callmed.app
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
class ProfessionModelTest {
 @Test fun medicalRegistrationUsesItsCouncil(){val d=Doctor.from(JSONObject("""{"id":1,"nome":"Fictício","especialidade":"Cardiologia","registro":"CRM 12345/SE"}"""));assertEquals("CRM 12345/SE",d.registro);assertFalse(d.registro.contains("CRP"))}
 @Test fun legacyPsychologyKeepsItsRegistration(){val d=Doctor.from(JSONObject("""{"id":1,"nome":"Fictício","especialidade":"Psicologia","crp":"12/12345"}"""));assertEquals("CRP 12/12345",d.registro)}
 @Test fun unknownRegistrationDoesNotInventACouncil(){val d=Doctor.from(JSONObject("""{"id":1,"nome":"Fictício","especialidade":"Clínica"}"""));assertEquals("Registro a conferir",d.registro)}
}
